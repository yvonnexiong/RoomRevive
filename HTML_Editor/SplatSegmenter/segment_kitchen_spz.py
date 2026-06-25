#!/usr/bin/env python3
"""Auto-recolor a kitchen Gaussian splat: countertop->limegreen, cabinet fronts->pink.

Geometry-first: per-splat normal = direction of the splat's smallest-scale axis.
Classify by 3D spatial REGION (confident-normal splats vote, region-grow recolors
the isotropic majority too) so the mask is solid instead of speckled.

Reads/writes gzip NGSP SPZ v2 (shDegree 0) in ORIGINAL byte order, then re-gzips so
the existing Unity LiveSplatLoader hot-reloads it. The source file is never modified.

Usage:
  python segment_kitchen_spz.py IN.spz OUT.spz [--diagnose] [--green-only]
                                [--debug-png path.png] [--voxel 0.05]
"""
import argparse, gzip, os, struct, sys
import numpy as np

# limegreen / pink as RAW color bytes. The Unity color curve (b/255-0.5)/0.15 -> SH0
# saturates, so these read as vivid green vs hot-pink and stay distinct in raw-byte space
# (green = G-dominant, pink = R+B high / G low) for any downstream byte-keyed detector.
LIMEGREEN = np.array([50, 205, 50], np.uint8)
PINK      = np.array([255, 45, 166], np.uint8)


def shdim(d): return {0: 0, 1: 3, 2: 8, 3: 15, 4: 24}.get(d, -1)


def load_spz(path):
    with open(path, 'rb') as f:
        raw = f.read()
    data = gzip.decompress(raw) if raw[:2] == b'\x1f\x8b' else raw
    if data[:4] != b'NGSP':
        raise ValueError('not an NGSP SPZ')
    ver, n = struct.unpack_from('<II', data, 4)
    shdeg, frac, flags = data[12], data[13], data[14]
    if ver != 2 or shdeg != 0:
        raise ValueError(f'this tool targets SPZ v2 / shDegree 0, got v{ver} shDeg{shdeg}')
    sd = shdim(shdeg)
    rotb = 4 if ver >= 3 else 3
    off = {}
    off['pos'] = 16
    off['alpha'] = off['pos'] + n * 9
    off['color'] = off['alpha'] + n
    off['scale'] = off['color'] + n * 3
    off['rot'] = off['scale'] + n * 3
    off['sh'] = off['rot'] + n * rotb
    expect = off['sh'] + n * sd * 3
    if len(data) != expect:
        raise ValueError(f'size mismatch: {len(data)} != {expect}')
    return dict(buf=bytearray(data), n=n, frac=frac, off=off, rotb=rotb, ver=ver)


def decode_geometry(spz):
    data = np.frombuffer(bytes(spz['buf']), np.uint8)
    n, off, frac = spz['n'], spz['off'], spz['frac']

    pb = data[off['pos']:off['pos'] + n * 9].reshape(n, 3, 3).astype(np.int32)
    pos_i = pb[..., 0] | (pb[..., 1] << 8) | (pb[..., 2] << 16)
    pos_i -= (pos_i & 0x800000 != 0) * 0x1000000
    pos = pos_i.astype(np.float32) / (1 << frac)

    sb = data[off['scale']:off['scale'] + n * 3].reshape(n, 3).astype(np.float32)
    scale = np.exp(sb / 16.0 - 10.0)

    rb = data[off['rot']:off['rot'] + n * 3].reshape(n, 3).astype(np.float32)
    xyz = rb / 127.5 - 1.0
    x, y, z = xyz[:, 0], xyz[:, 1], xyz[:, 2]
    w = np.sqrt(np.clip(1.0 - (x * x + y * y + z * z), 0.0, None))

    # columns of the rotation matrix = world dir of each local axis
    cols = np.stack([
        np.stack([1 - 2 * (y * y + z * z), 2 * (x * y + w * z), 2 * (x * z - w * y)], 1),
        np.stack([2 * (x * y - w * z), 1 - 2 * (x * x + z * z), 2 * (y * z + w * x)], 1),
        np.stack([2 * (x * z + w * y), 2 * (y * z - w * x), 1 - 2 * (x * x + y * y)], 1),
    ], 1)  # (n,3axes,3xyz)

    kmin = np.argmin(scale, 1)
    normal = cols[np.arange(n), kmin]
    normal /= np.linalg.norm(normal, axis=1, keepdims=True) + 1e-9

    ssort = np.sort(scale, 1)
    aniso = ssort[:, 0] / (ssort[:, 1] + 1e-9)  # thin/ mid : low => flat disk => trustworthy normal
    return pos, normal, scale, aniso


def estimate_up_axis(normal, confident):
    n = normal[confident]
    M = n.T @ n
    _, evecs = np.linalg.eigh(M)
    up = evecs[:, -1]
    return int(np.argmax(np.abs(up)))


def core_mask(pos, plane_axes, keep=0.9):
    m = np.ones(len(pos), bool)
    for a in plane_axes:
        lo, hi = np.percentile(pos[:, a], [(1 - keep) * 50, 100 - (1 - keep) * 50])
        m &= (pos[:, a] >= lo) & (pos[:, a] <= hi)
    return m


def scatter_rgb(layers, ax_x, ax_y, W=1000, H=700):
    """layers: list of (pts(N,3), (b,g,r)). First layer is background density (gray)."""
    allx = np.concatenate([l[0][:, ax_x] for l in layers])
    ally = np.concatenate([l[0][:, ax_y] for l in layers])
    xmin, xmax = np.percentile(allx, [0.2, 99.8])
    ymin, ymax = np.percentile(ally, [0.2, 99.8])
    img = np.zeros((H, W, 3), np.float32)

    def acc(pts, col, gamma):
        x = pts[:, ax_x]; y = pts[:, ax_y]
        gx = np.clip(((x - xmin) / (xmax - xmin + 1e-9) * (W - 1)), 0, W - 1).astype(int)
        gy = np.clip(((ymax - y) / (ymax - ymin + 1e-9) * (H - 1)), 0, H - 1).astype(int)
        grid = np.zeros((H, W), np.float32)
        np.add.at(grid, (gy, gx), 1.0)
        grid = np.power(grid / (grid.max() + 1e-9), gamma)
        for c in range(3):
            img[:, :, c] = np.maximum(img[:, :, c], grid * col[c])

    for i, (pts, col) in enumerate(layers):
        acc(pts, col, 0.7 if i == 0 else 0.5)
    return np.clip(img, 0, 255).astype(np.uint8)


def render_view(pos, bgr, label, ax_x, ax_y, crop=None, W=950, H=680):
    """Real splat colors (dim) as background; green/pink label splats painted bright on top."""
    sel = np.ones(len(pos), bool)
    if crop:
        for a, (lo, hi) in crop.items():
            sel &= (pos[:, a] >= lo) & (pos[:, a] <= hi)
    P, B, L = pos[sel], bgr[sel], label[sel]
    x, y = P[:, ax_x], P[:, ax_y]
    xmin, xmax = np.percentile(x, [0.3, 99.7])
    ymin, ymax = np.percentile(y, [0.3, 99.7])
    gx = np.clip((x - xmin) / (xmax - xmin + 1e-9) * (W - 1), 0, W - 1).astype(int)
    gy = np.clip((ymax - y) / (ymax - ymin + 1e-9) * (H - 1), 0, H - 1).astype(int)
    acc = np.zeros((H, W, 3), np.float32)
    cnt = np.zeros((H, W), np.float32)
    order = np.argsort(L)  # draw unlabeled first, labels last
    np.add.at(acc, (gy, gx), B.astype(np.float32))
    np.add.at(cnt, (gy, gx), 1.0)
    img = (acc / np.maximum(cnt[..., None], 1)) * 0.5
    lg = np.zeros((H, W), np.uint8)
    for lv in (1, 2):
        m = L == lv
        lg[gy[m], gx[m]] = lv
    img8 = np.clip(img, 0, 255).astype(np.uint8)
    img8[lg == 1] = (60, 255, 60)
    img8[lg == 2] = (200, 50, 255)
    return img8


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('inp'); ap.add_argument('out', nargs='?')
    ap.add_argument('--diagnose', action='store_true')
    ap.add_argument('--green-only', action='store_true')
    ap.add_argument('--aniso', type=float, default=0.5)
    ap.add_argument('--debug-dir', default='')
    ap.add_argument('--dump-bands', action='store_true')
    args = ap.parse_args()

    spz = load_spz(args.inp)
    print(f'loaded {os.path.basename(args.inp)}: {spz["n"]:,} splats, frac={spz["frac"]}')
    pos, normal, scale, aniso = decode_geometry(spz)
    AX = ["X", "Y", "Z"]
    _co = spz['off']['color']
    orig_bgr = np.frombuffer(bytes(spz['buf'][_co:_co + spz['n'] * 3]), np.uint8).reshape(spz['n'], 3)[:, ::-1].copy()

    confident = aniso < args.aniso
    print(f'confident-normal splats (aniso<{args.aniso}): {confident.sum():,} ({100*confident.mean():.1f}%)')

    up_axis = estimate_up_axis(normal, confident)
    plane_axes = [i for i in range(3) if i != up_axis]
    print(f'up axis = {AX[up_axis]}   plane axes = {AX[plane_axes[0]]},{AX[plane_axes[1]]}')

    core = core_mask(pos, plane_axes, keep=0.9)
    print(f'dense core (central 90% in plane): {core.sum():,} ({100*core.mean():.1f}%)')

    u = pos[:, up_axis]
    ndotup = np.abs(normal[:, up_axis])
    is_h = confident & (ndotup > 0.85)
    is_v = confident & (ndotup < 0.35)
    print(f'confident horizontal seeds: {is_h.sum():,}   confident vertical seeds: {is_v.sum():,}')

    a0, a1 = plane_axes
    pmin = np.array([np.percentile(pos[core, a0], 0.3), np.percentile(pos[core, a1], 0.3)])
    pmax = np.array([np.percentile(pos[core, a0], 99.7), np.percentile(pos[core, a1], 99.7)])
    cell = max((pmax - pmin)) / 200.0  # ~ a few cm
    GW = int((pmax[0] - pmin[0]) / cell) + 3
    GH = int((pmax[1] - pmin[1]) / cell) + 3

    def cells_of(mask):
        g0 = np.clip(((pos[mask, a0] - pmin[0]) / cell).astype(int), 0, GW - 1)
        g1 = np.clip(((pos[mask, a1] - pmin[1]) / cell).astype(int), 0, GH - 1)
        return g0, g1

    def cells_all():
        g0 = np.clip(((pos[:, a0] - pmin[0]) / cell).astype(int), 0, GW - 1)
        g1 = np.clip(((pos[:, a1] - pmin[1]) / cell).astype(int), 0, GH - 1)
        return g0, g1

    # --- bands by occupied footprint area ---
    lo, hi = np.percentile(u[core], [0.5, 99.5])
    edges = np.linspace(lo, hi, 65)
    hseed = is_h & core
    bidx = np.digitize(u[hseed], edges) - 1
    hg0, hg1 = cells_of(hseed)
    bands = []
    for b in range(64):
        sel = bidx == b
        if sel.sum() < 200:
            continue
        area = len(set(zip(hg0[sel].tolist(), hg1[sel].tolist())))
        bands.append([(edges[b] + edges[b + 1]) / 2, int(sel.sum()), area])
    bands = np.array(bands) if bands else np.zeros((0, 3))
    if args.dump_bands:
        print('bands (center | count | footprint-cells):')
        for c, n_, a in bands:
            print(f'  {c:8.3f} | {int(n_):7d} | {int(a):5d}')
    import cv2
    from scipy import ndimage

    def occ_grid(mask, close=5, dilate=0):
        g0, g1 = cells_of(mask)
        g = np.zeros((GH, GW), np.uint8)
        g[g1, g0] = 1
        if close:
            g = cv2.morphologyEx(g, cv2.MORPH_CLOSE, np.ones((close, close), np.uint8))
        if dilate:
            g = cv2.dilate(g, np.ones((dilate, dilate), np.uint8))
        return g

    # floor/ceiling = lowest/highest band with substantial footprint (floor footprint can dwarf ceiling)
    big = bands[bands[:, 2] >= 0.30 * bands[:, 2].max()]
    floor_y = float(big[:, 0].min())
    ceil_y = float(big[:, 0].max())
    room_h = ceil_y - floor_y
    HB = 0.045 * room_h

    # counter = mid-height slab with the most VERTICAL (cabinet) mass directly below it.
    # A table has only legs below; a real countertop has a wall of cabinet fronts. That
    # cabinet-support score disambiguates the counter from shelves/tables at similar heights.
    vcore = is_v & core
    vu = u[vcore]
    vg0, vg1 = cells_of(vcore)
    cand = bands[(bands[:, 0] > floor_y + 0.12 * room_h) & (bands[:, 0] < ceil_y - 0.12 * room_h)]
    counter_y, best = (float(cand[0, 0]) if len(cand) else (floor_y + ceil_y) / 2), -1.0
    for yc, cnt, area in cand:
        fp = occ_grid(is_h & core & (np.abs(u - yc) < HB), close=5, dilate=3)
        win_lo = max(floor_y + 0.03 * room_h, yc - 0.35 * room_h)  # fixed window so tall slabs aren't rewarded
        below = (vu > win_lo) & (vu < yc - 0.02 * room_h)
        support = int(fp[vg1[below], vg0[below]].sum())
        fill = support / max(int(fp.sum()), 1)  # cabinet density per footprint cell, not raw mass
        rel = (yc - floor_y) / room_h
        prior = float(np.exp(-((rel - 0.33) / 0.15) ** 2))  # counters sit ~1/3 up the room, very stable
        score = fill * np.sqrt(max(area, 1)) * prior
        if args.dump_bands:
            print(f'  cand y={yc:7.3f} rel={rel:.2f} area={int(area):5d} fill={fill:5.1f} prior={prior:.2f} score={score:8.1f}')
        if score > best:
            counter_y, best = float(yc), score
    print(f'\nfloor Y={floor_y:.3f}  counter Y={counter_y:.3f}  ceiling Y={ceil_y:.3f}  '
          f'room_h={room_h:.3f}  HB={HB:.3f}')

    # --- cabinet-mass map: vertical seeds in the under-counter window, per cell ---
    cabwin = is_v & core & (u > floor_y + 0.02 * room_h) & (u < counter_y - 0.02 * room_h)
    cw0, cw1 = cells_of(cabwin)
    vsupp = np.zeros((GH, GW), np.float32)
    np.add.at(vsupp, (cw1, cw0), 1.0)
    vsupp = cv2.boxFilter(vsupp, -1, (5, 5), normalize=False)

    # --- counter footprint: band blobs that ACTUALLY have cabinets under them ---
    # Use all band splats for a dense footprint; keep a blob only if it is large enough AND has
    # real cabinet mass beneath it. That support test drops counter-height clutter (sills, a high
    # table) that no cabinet supports, cleaning green and pink together.
    occ = occ_grid(core & (np.abs(u - counter_y) < HB), close=9)
    lab, num = ndimage.label(occ, np.ones((3, 3)))
    vcov = (vsupp >= 2.0).astype(np.float32)  # a cell with real cabinet mass below it
    csize = ndimage.sum(np.ones_like(lab), lab, range(1, num + 1))
    cfrac = ndimage.sum(vcov, lab, range(1, num + 1)) / np.maximum(csize, 1)  # share of blob with cabinets under it
    keep = [i + 1 for i in range(num) if csize[i] >= 0.05 * csize.max() and cfrac[i] >= 0.12]
    if args.dump_bands:
        for i in range(num):
            if csize[i] >= 0.02 * csize.max():
                print(f'  comp {i+1:2d}: size={int(csize[i]):5d} cfrac={cfrac[i]:.2f} '
                      f'{"KEEP" if (i + 1) in keep else "drop"}')
    counter_grid = cv2.dilate(np.isin(lab, keep).astype(np.uint8), np.ones((5, 5), np.uint8))
    print(f'counter components kept: {len(keep)}/{num}  footprint cells: {int(counter_grid.sum())}')

    # --- wall columns: vertical seeds that rise well above the counter (floor->ceiling) ---
    vcore = is_v & core
    vg0, vg1 = cells_of(vcore)
    above = u[vcore] > counter_y + 0.12 * room_h
    wall_grid = np.zeros((GH, GW), np.uint8)
    wall_grid[vg1[above], vg0[above]] = 1
    wall_grid = cv2.dilate(wall_grid, np.ones((3, 3), np.uint8))

    # cabinet = under the kept counter, not a thru-wall column, with real cabinet mass present
    cabinet_grid = ((counter_grid > 0) & (wall_grid == 0) & (vsupp >= 2.0)).astype(np.uint8)

    # --- assign labels to ALL splats by region (region-grow over the isotropic majority) ---
    ag0, ag1 = cells_all()
    label = np.zeros(spz['n'], np.uint8)  # 0 none, 1 counter, 2 cabinet
    counter_sel = (np.abs(u - counter_y) < HB) & core & (counter_grid[ag1, ag0] > 0)
    label[counter_sel] = 1
    cab_sel = core & (u > floor_y + 0.02 * room_h) & (u < counter_y - 0.02 * room_h) & (cabinet_grid[ag1, ag0] > 0)
    if args.green_only:
        cab_sel[:] = False
    label[cab_sel] = 2
    print(f'labelled  countertop: {(label==1).sum():,}   cabinet: {(label==2).sum():,}')

    if args.debug_dir:
        os.makedirs(args.debug_dir, exist_ok=True)
        ys, xs = np.nonzero(counter_grid)
        mx = 0.6 * (xs.max() - xs.min()) * cell + 1e-3
        mz = 0.6 * (ys.max() - ys.min()) * cell + 1e-3
        x_lo, x_hi = pmin[0] + xs.min() * cell - mx, pmin[0] + xs.max() * cell + mx
        z_lo, z_hi = pmin[1] + ys.min() * cell - mz, pmin[1] + ys.max() * cell + mz
        y_lo, y_hi = floor_y - 0.05 * room_h, ceil_y + 0.05 * room_h
        cropF = {a1: (z_lo, z_hi), up_axis: (y_lo, y_hi)}
        cropS = {a0: (x_lo, x_hi), up_axis: (y_lo, y_hi)}
        cropP = {a0: (x_lo, x_hi), a1: (z_lo, z_hi)}
        for tag, axx, axy, crop in [('front_%s%s' % (AX[a0], AX[up_axis]), a0, up_axis, cropF),
                                    ('side_%s%s' % (AX[a1], AX[up_axis]), a1, up_axis, cropS),
                                    ('plan_%s%s' % (AX[a0], AX[a1]), a0, a1, cropP)]:
            cv2.imwrite(os.path.join(args.debug_dir, tag + '.png'),
                        render_view(pos, orig_bgr, label, axx, axy, crop))
        print('wrote label previews to', args.debug_dir)

    if args.diagnose or not args.out:
        print('\n[diagnose] no file written.')
        return

    # --- recolor in original byte order, re-gzip ---
    buf = spz['buf']
    co = spz['off']['color']
    color = np.frombuffer(bytes(buf[co:co + spz['n'] * 3]), np.uint8).reshape(spz['n'], 3).copy()
    color[label == 1] = LIMEGREEN
    color[label == 2] = PINK
    buf[co:co + spz['n'] * 3] = color.tobytes()
    out = gzip.compress(bytes(buf), 6)
    with open(args.out, 'wb') as f:
        f.write(out)
    print(f'wrote {args.out}  ({len(out)/1e6:.1f} MB)  '
          f'green={int((label==1).sum()):,} pink={int((label==2).sum()):,}')


if __name__ == '__main__':
    main()
