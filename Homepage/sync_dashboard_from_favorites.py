#!/usr/bin/env python3
"""
Rebuild the Homepage dashboard's "Saved" list (window.RR_PRODUCTS) from
HTML_Editor/admin/favorites.json.

Each favorite { id, modelKey } is looked up in catalog.json; a product card is
generated with full data + hero/gallery images. Image `fit` is chosen per image
so the square gallery frame is always filled vertically (no top/bottom white):
  - wider than tall  -> "cover"   (zoom, crop sides)
  - taller than wide -> "contain" (scale down, side padding)

Re-run this after favoriting/unfavoriting to refresh the dashboard.

Usage:  python sync_dashboard_from_favorites.py
"""
import re, json, gzip, base64, os, sys, io, urllib.parse
from PIL import Image

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

HERE   = os.path.dirname(os.path.abspath(__file__))
REPO   = os.path.abspath(os.path.join(HERE, '..'))
HTML   = os.path.join(HERE, 'RoomRevive Dashboard (RoomCapture before).html')
FAVS   = os.path.join(REPO, 'HTML_Editor', 'admin', 'favorites.json')
CATLG  = os.path.join(REPO, 'HTML_Editor', 'admin', 'catalog.json')
MODELS = os.path.join(REPO, 'HTML_Editor', '3D-models')
UUID   = '58ff87c8-3f3d-411c-9d68-9c3485dc04b9'   # data.js asset

CAT_SINGULAR = {
    'Dishwashers': 'Dishwasher', 'Fridges': 'Fridge', 'Cooktops': 'Cooktop',
    'Hoods': 'Hood', 'Microwaves': 'Microwave', 'CoffeeMachines': 'Coffee Machine',
    'Kitchens': 'Kitchen',
}
LABELS = ['interior', 'detail', 'context', 'controls']


def fit_for(disk_path):
    try:
        with Image.open(disk_path) as im:
            w, h = im.size
    except Exception:
        return 'contain'
    return 'cover' if w > h else 'contain'   # square frame -> always fill vertical


def build_images(category, folder):
    """hero + up to 4 gallery, each with aspect-correct fit. [] if no render."""
    base_disk = os.path.join(MODELS, category, folder)
    base_rel  = f'../HTML_Editor/3D-models/{category}/{folder}'
    hero = os.path.join(base_disk, 'hero.webp')
    if not os.path.isfile(hero):
        return [], None
    imgs = [{'src': f'{base_rel}/hero.webp', 'label': 'front', 'fit': fit_for(hero)}]
    gdir = os.path.join(base_disk, 'gallery')
    if os.path.isdir(gdir):
        gal = sorted([g for g in os.listdir(gdir) if g.endswith('.webp')],
                     key=lambda s: int(re.search(r'g(\d+)', s).group(1)))
        for i, g in enumerate(gal[:4]):
            imgs.append({'src': f'{base_rel}/gallery/{g}',
                         'label': LABELS[i] if i < len(LABELS) else f'view {i+2}',
                         'fit': fit_for(os.path.join(gdir, g))})
    return imgs, f'{base_rel}/hero.webp'


def num(v, unit):
    return f'{v} {unit}' if v not in (None, '') else None


def build_product_data(category, p):
    rows = [('Model', p.get('name')), ('Part No.', p.get('sku'))]
    if category == 'Fridges':
        rows += [('Energy class', p.get('energyClass')),
                 ('Fridge capacity', num(p.get('fridgeCapacity'), 'L')),
                 ('Freezer capacity', num(p.get('freezerCapacity'), 'L')),
                 ('Annual energy use', num(p.get('annualEnergy'), 'kWh')),
                 ('Noise level', num(p.get('noise'), 'dB'))]
    elif category == 'Dishwashers':
        rows += [('Energy class', p.get('energyClass')),
                 ('Place settings', p.get('placeSettings')),
                 ('Water per cycle', num(p.get('waterPerCycle'), 'L')),
                 ('Noise class', p.get('noiseClass'))]
    elif category == 'Cooktops':
        rows += [('Zones', p.get('zones')),
                 ('Induction', 'Yes' if p.get('induction') else None),
                 ('Total power', num(p.get('totalPowerKw'), 'kW'))]
    elif category == 'Hoods':
        rows += [('Energy class', p.get('energyClass')),
                 ('Airflow', num(p.get('airflow'), 'm³/h')),
                 ('Noise', num(p.get('noise'), 'dB')),
                 ('Annual energy', num(p.get('annualEnergy'), 'kWh'))]
    elif category == 'Microwaves':
        rows += [('Capacity', num(p.get('capacityL'), 'L')),
                 ('Power', num(p.get('microwavePowerW'), 'W'))]
    rows += [('Dimensions', p.get('dimensions')), ('Colour', p.get('color'))]
    return {k: str(v) for k, v in rows if v not in (None, '')}


def build_entry(fav, item):
    p = item['product']
    category = item['category']
    cat_name = CAT_SINGULAR.get(category, category)
    folder = fav.get('modelKey') or (item.get('file', '') or '').replace('.glb', '')
    imgs, thumb = build_images(category, folder)
    rs = [r['rating'] for r in p.get('reviews', [])]
    rating = round(sum(rs) / len(rs) * 2) / 2 if rs else 4.5
    entry = {
        'id': 'fav-' + fav['id'][:10],
        'name': p['name'], 'series': p.get('color', ''), 'variant': p.get('color', ''),
        'category': cat_name, 'manufacturer': p.get('brand', 'Miele'),
        'mieleUrl': p.get('productPageUrl', ''),
        'price': p.get('price'), 'priceNote': 'incl. VAT',
        'savedOn': '2026-06-23T15:30:00Z',
        'rating': rating, 'reviewCount': p.get('reviewCount', len(rs)),
        'fromCatalog': fav['id'], 'modelKey': folder,
        'swatch': 'linear-gradient(120deg, #DCE0E6 0%, #C7CCD2 50%, #AAB0BA 100%)',
        'delivery': ['Plus delivery costs', 'Delivery within 5–7 working days',
                     'Old appliance collection & disposal free of charge'],
        'blurb': f"{p.get('subtitle','')}. {p.get('headline','')}.".strip('. ') + '.',
        'features': [{'name': f, 'desc': ''} for f in p.get('features', [])][:5],
        'productData': build_product_data(category, p),
        'reviews': [{'stars': r['rating'], 'date': r['date'], 'author': r['author'],
                     'location': f"Reviewed in {r['country']}", 'text': r['body']}
                    for r in p.get('reviews', [])],
        'images': imgs, 'gallery': [],
    }
    if thumb:
        entry['thumb'] = thumb
    return entry, bool(imgs)


def to_js(obj, indent=2):
    s = json.dumps(obj, ensure_ascii=False, indent=2)
    pad = ' ' * indent
    return '\n'.join(pad + ln for ln in s.split('\n'))


def main():
    favs = json.load(open(FAVS, encoding='utf-8'))['favorites']
    catalog = json.load(open(CATLG, encoding='utf-8'))
    byid = {i['id']: i for i in catalog['items']}

    entries, no_img = [], []
    for fav in favs:
        item = byid.get(fav['id'])
        if not item:
            print(f"  ! skip {fav['id']} (not in catalog)")
            continue
        entry, has_img = build_entry(fav, item)
        entries.append(entry)
        if not has_img:
            no_img.append(entry['name'])

    html = open(HTML, encoding='utf-8').read()
    m = re.search(r'<script type="__bundler/manifest">(.*?)</script>', html, re.S)
    manifest = json.loads(m.group(1))
    old_b64 = manifest[UUID]['data']
    data = gzip.decompress(base64.b64decode(old_b64)).decode('utf-8')

    # replace the entire RR_PRODUCTS array
    start = data.index('window.RR_PRODUCTS')
    arr_open = data.index('[', start)
    end = data.index('window.RR_RECOMMENDATIONS')
    arr_close = data.rindex('];', arr_open, end) + 2
    body = ',\n'.join(to_js(e) for e in entries)
    # `window.__LIVE_PRODUCTS ||` : when the page is served by the admin server,
    # an injected boot script sets window.__LIVE_PRODUCTS from /api/dashboard.json
    # and that wins. Opened as a plain file:// this falls back to the baked list.
    new_block = f'window.RR_PRODUCTS = window.__LIVE_PRODUCTS || [\n{body}\n];'
    data2 = data[:start] + new_block + data[arr_close:]

    new_b64 = base64.b64encode(gzip.compress(data2.encode('utf-8'))).decode('ascii')
    assert html.count(old_b64) == 1
    open(HTML, 'w', encoding='utf-8').write(html.replace(old_b64, new_b64, 1))

    print(f"Dashboard rebuilt from favorites.json: {len(entries)} products")
    for e in entries:
        print(f"  - {e['category']:14s} {e['name']:22s} imgs:{len(e['images'])}")
    if no_img:
        print(f"  (no render on disk, shown without image: {', '.join(no_img)})")


if __name__ == '__main__':
    main()
