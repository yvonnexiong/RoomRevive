import json, base64, gzip, os
ROOT = 'C:/Unity-Git/RoomRevive/'
cat = json.load(open(os.path.join(ROOT, 'HTML_Editor/admin/catalog.json'), encoding='utf-8'))

PICKS = {'Dishwashers':'G 5632 SCU Active S','Fridges':'KFN 4797 CD','Cooktops':'KM 7363 FL',
         'Hoods':'DAH 4980 Sienna','CoffeeMachines':'CM5310','Kitchens':'CASCADA 774'}
CAT_LABEL = {'Dishwashers':'Dishwasher','Fridges':'Fridge','Cooktops':'Cooktop','Hoods':'Hood',
             'CoffeeMachines':'Coffee Machine','Kitchens':'Kitchen'}
SAVED = {'Dishwashers':'2026-06-24T09:10:00Z','Fridges':'2026-06-24T09:20:00Z','Cooktops':'2026-06-24T09:30:00Z',
         'Hoods':'2026-06-24T09:40:00Z','CoffeeMachines':'2026-06-24T09:50:00Z','Kitchens':'2026-06-24T10:00:00Z'}
LABELS = ['front','angle','detail','interior','controls','open','context','side','top','rear']
LABEL_MAP = [('placeSettings','Place settings',''),('fridgeCapacity','Fridge capacity',' L'),('freezerCapacity','Freezer capacity',' L'),
 ('capacityL','Capacity',' L'),('zones','Cooking zones',''),('induction','Induction','BOOL'),('totalPowerKw','Total power',' kW'),
 ('boosterKw','Booster',' kW'),('airflow','Extraction rate',' m³/h'),('waterTank','Water tank',' L'),('beanContainer','Bean container',' g'),
 ('milkContainer','Milk container',' L'),('energyClass','Energy class',''),('noise','Noise',' dB'),('noiseClass','Noise class',''),
 ('annualEnergy','Annual energy',' kWh'),('energyPer100Cycles','Energy / 100 cycles',' kWh'),('waterPerCycle','Water per cycle',' L'),
 ('weightKg','Weight',' kg'),('kitchenType','Style',''),('front','Front',''),('carcase','Carcase',''),('worktop','Worktop',''),('handle','Handle',''),('dimensions','Dimensions','')]

def relimg(p):
    p = (p or '').replace('\\','/')
    return '../' + p[len(ROOT):] if p.startswith(ROOT) else p

def chip_for(color, sw):
    c = (color or '').lower()
    table = [('blackberry','linear-gradient(135deg,#7a2230,#3d0f18)'),('obsidian','linear-gradient(135deg,#3a3d42,#15171a)'),
        ('black','linear-gradient(135deg,#3a3d42,#15171a)'),('graphite','linear-gradient(135deg,#6b6e74,#3c3f44)'),
        ('grey','linear-gradient(135deg,#9a9ea6,#5c5f66)'),('gray','linear-gradient(135deg,#9a9ea6,#5c5f66)'),
        ('steel','linear-gradient(135deg,#DDE0E6,#A9AFB9)'),('stainless','linear-gradient(135deg,#DDE0E6,#A9AFB9)'),
        ('lotus','linear-gradient(135deg,#FFFFFF,#EDEDEA)'),('white','linear-gradient(135deg,#FFFFFF,#E4E6EA)'),
        ('pearl','linear-gradient(135deg,#EDE7DC,#D2C7B4)'),('sienna','linear-gradient(135deg,#C98A5E,#9c633c)'),
        ('beige','linear-gradient(135deg,#E8DFD0,#C9BBA3)'),('blue','linear-gradient(135deg,#6b86a3,#3f5a78)'),('red','linear-gradient(135deg,#7a2230,#3d0f18)')]
    for k,g in table:
        if k in c: return g
    if sw: return 'linear-gradient(135deg, %s 0%%, %s 100%%)' % (sw, sw)
    return 'linear-gradient(135deg,#D7DAE0,#AAB0BA)'

def cid(color, i):
    c = (color or '').lower()
    for k in ['blackberry','obsidian','black','graphite','steel','stainless','lotus','white','pearl','sienna','beige','blue']:
        if k in c: return k
    return 'c%d' % i

def imgs_for(p):
    out = [{'src':relimg(p['heroImage']),'label':'front','fit':'contain'}]
    for k,g in enumerate(p.get('additionalImages') or []):
        out.append({'src':relimg(g),'label':LABELS[(k+1) % len(LABELS)],'fit':'contain'})
    return out

def prefix(brand):
    b = (brand or '').lower()
    return 'mie-' if 'miele' in b else ('nob-' if 'nobilia' in b else 'cat-')

built = []
for category, vg in PICKS.items():
    def gkey(it):
        p = it.get('product') or {}
        return p.get('variantGroup') or p.get('name') or it.get('name')
    variants = [i for i in cat['items'] if i.get('product', {}).get('heroImage') and gkey(i)==vg]
    assert variants, 'no variants for ' + vg
    for it in variants:
        for img in [it['product']['heroImage']] + (it['product'].get('additionalImages') or []):
            assert os.path.exists(img.replace('\\','/')), 'MISSING ' + img
    d = variants[0]['product']
    fopts = []
    for i, it in enumerate(variants):
        p = it['product']
        fopts.append({'id':cid(p.get('color'), i),'label':p.get('color') or 'Default','chip':chip_for(p.get('color'), p.get('swatchColor')),
                      'thumb':relimg(p['heroImage']),'images':imgs_for(p),'price':p.get('price'),'sku':p.get('sku'),
                      'catalogId':it['id'],'mieleUrl':p.get('productPageUrl')})
    dimg = imgs_for(d)
    pd = {'Model':d.get('name'),'Part No.':d.get('sku')}
    for key, lbl, unit in LABEL_MAP:
        if d.get(key) not in (None,'',[]):
            v = d[key]
            pd[lbl] = ('Yes' if v else 'No') if unit=='BOOL' else (str(v) + unit)
    pd['Colour'] = d.get('color')
    blurb = (d.get('description') or d.get('headline') or '')
    if d.get('emotionalLine'): blurb = (blurb + ' ' + d['emotionalLine']).strip()
    revs = [{'stars':r.get('rating'),'date':r.get('date'),'author':r.get('author'),
             'location':('Reviewed in ' + r['country']) if r.get('country') else None,'text':r.get('body')} for r in (d.get('reviews') or [])]
    obj = {'id':prefix(d.get('brand')) + vg.lower().replace(' ','').replace('-',''),'name':d.get('name'),
           'series':d.get('color'),'variant':d.get('color'),'category':CAT_LABEL[category],'manufacturer':d.get('brand'),
           'mieleUrl':d.get('productPageUrl'),'price':d.get('price'),'priceNote':'incl. VAT' if d.get('price') else None,
           'priceLabel':None if d.get('price') else (d.get('priceLabel') or 'Price on request'),'savedOn':SAVED[category],
           'rating':d.get('reviewStars') or d.get('reviewScore') or 5,'reviewCount':d.get('reviewCount') or len(revs),
           'fromCatalog':[it['id'] for it in variants],'mergedColors':[it['product'].get('color') for it in variants],
           'swatch':chip_for(d.get('color'), d.get('swatchColor')),
           'delivery':['Plus delivery costs','Delivery within 5–7 working days','Old appliance collection & disposal free of charge'],
           'blurb':blurb,'features':[{'name':f,'desc':''} for f in (d.get('features') or [])],'productData':pd,'reviews':revs,
           'thumb':relimg(d['heroImage']),'thumbFit':'cover','images':dimg,'finishOptions':fopts,'gallery':[im['label'] for im in dimg]}
    if obj['price'] is None: obj.pop('priceNote', None)
    else: obj.pop('priceLabel', None)
    built.append(obj)
    print('built %-13s %-22s colors=%d imgs=%d price=%s' % (category, vg, len(variants), len(dimg), obj['price']))

DST = os.path.join(ROOT, 'Homepage/RoomRevive Dashboard (RoomCapture before).html')
lines = open(DST, 'r', encoding='utf-8').read().split('\n')
man = json.loads(lines[172]); DUUID = '58ff87c8-3f3d-411c-9d68-9c3485dc04b9'
js = gzip.decompress(base64.b64decode(man[DUUID]['data'])).decode('utf-8')

def match_close(s, open_idx, oc, cc):
    depth=0; instr=False; esc=False
    for k in range(open_idx, len(s)):
        ch=s[k]
        if instr:
            if esc: esc=False
            elif ch=='\\': esc=True
            elif ch=='"': instr=False
        else:
            if ch=='"': instr=True
            elif ch==oc: depth+=1
            elif ch==cc:
                depth-=1
                if depth==0: return k
    return -1

b = js.index('[', js.index('window.RR_PRODUCTS'))
bend = match_close(js, b, '[', ']')
inner = js[b+1:bend]
# split top-level objects in the array
objs=[]; depth=0; instr=False; esc=False; cur=None
for k in range(len(inner)):
    ch=inner[k]
    if instr:
        if esc: esc=False
        elif ch=='\\': esc=True
        elif ch=='"': instr=False
    else:
        if ch=='"': instr=True
        elif ch=='{':
            if depth==0: cur=k
            depth+=1
        elif ch=='}':
            depth-=1
            if depth==0: objs.append(inner[cur:k+1])
keep = [o for o in objs if ('mie-m7244tc' in o) or ('senso494' in o) or ('riva893' in o)]
print('keeping', len(keep), 'existing (m7244tc + cabinets)')
new = [json.dumps(o, ensure_ascii=False, indent=2).replace('\n','\n  ') for o in built]
assembled = '\n  ' + ',\n  '.join(new + keep) + '\n'
js2 = js[:b+1] + assembled + js[bend:]

man[DUUID]['data'] = base64.b64encode(gzip.compress(js2.encode('utf-8'))).decode('ascii')
lines[172] = json.dumps(man, separators=(',',':'), ensure_ascii=False)
open(DST, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
print('balanced braces', js2.count('{')==js2.count('}'), 'brackets', js2.count('[')==js2.count(']'))
print('total products now:', len(built) + len(keep))
