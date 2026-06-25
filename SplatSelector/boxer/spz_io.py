"""SPZ v1/v2 codec ported from pink_to_marble_editor.html (parseSPZ).

The .spz file is gzip-compressed. Layout after a 16-byte header:
  positions (3x int24, fixed-point) | alpha (u8) | color (3x u8) |
  scales (3x u8, log) | quat (3x u8, xyz; w reconstructed) | SH (optional)
"""
import gzip
import numpy as np

SPZ_MAGIC = 0x5053474E
SH_C0 = 0.28209479177387814
SH_DIMS = {0: 0, 1: 9, 2: 24, 3: 45}


def parse_spz(path):
    with open(path, "rb") as f:
        raw = f.read()
    if raw[:2] == b"\x1f\x8b":  # gzip magic
        raw = gzip.decompress(raw)
    b = np.frombuffer(raw, dtype=np.uint8)
    dv = raw

    magic = int.from_bytes(dv[0:4], "little")
    if magic != SPZ_MAGIC:
        raise ValueError("Not an SPZ file (bad magic)")
    version = int.from_bytes(dv[4:8], "little")
    if version not in (1, 2):
        raise ValueError(f"Unsupported SPZ version {version}")
    n = int.from_bytes(dv[8:12], "little")
    sh_degree = dv[12]
    frac_bits = dv[13]
    flags = dv[14]
    off = 16

    # positions: n*3 little-endian int24, fixed point
    pos_bytes = b[off:off + n * 3 * 3].astype(np.int32).reshape(n * 3, 3)
    v = pos_bytes[:, 0] | (pos_bytes[:, 1] << 8) | (pos_bytes[:, 2] << 16)
    v = np.where(v & 0x800000, v - 0x1000000, v)
    positions = (v.astype(np.float64) / (1 << frac_bits)).astype(np.float32).reshape(n, 3)
    off += n * 9

    alphas = b[off:off + n].copy(); off += n  # raw u8 opacity (0..255)

    color_bytes = b[off:off + n * 3].astype(np.float64); off += n * 3
    colors = np.clip(0.5 + ((color_bytes / 255 - 0.5) / 0.15) * SH_C0, 0, 1)
    colors = colors.astype(np.float32).reshape(n, 3)

    scales_log = (b[off:off + n * 3].astype(np.float32) / 16 - 10).reshape(n, 3); off += n * 3

    q = b[off:off + n * 3].astype(np.float32).reshape(n, 3) / 127.5 - 1.0; off += n * 3
    w = np.sqrt(np.clip(1 - (q ** 2).sum(axis=1), 0, None))
    quats = np.column_stack([w, q[:, 0], q[:, 1], q[:, 2]]).astype(np.float32)  # wxyz

    return dict(n=n, sh_degree=sh_degree, frac_bits=frac_bits, flags=flags,
                positions=positions, alphas=alphas, colors=colors,
                scales_log=scales_log, quats=quats)


def parse_ply(path):
    """Parse a binary_little_endian 3DGS PLY file into the same dict format as parse_spz."""
    with open(path, "rb") as f:
        raw = f.read()

    # locate end_header
    for sep in (b"end_header\n", b"end_header\r\n"):
        pos = raw.find(sep)
        if pos != -1:
            data_off = pos + len(sep)
            break
    else:
        raise ValueError("PLY: end_header not found")

    header = raw[:pos].decode("ascii", errors="replace")
    lines = [l.strip() for l in header.split("\n") if l.strip()]

    fmt = next((l for l in lines if l.startswith("format")), "")
    if "binary_little_endian" not in fmt:
        raise ValueError("PLY: only binary_little_endian supported")

    n = int(next(l for l in lines if l.startswith("element vertex")).split()[2])

    NP = {
        "char": "i1", "uchar": "u1", "short": "<i2", "ushort": "<u2",
        "int": "<i4", "uint": "<u4", "float": "<f4", "double": "<f8",
        "int8": "i1", "uint8": "u1", "int16": "<i2", "uint16": "<u2",
        "int32": "<i4", "uint32": "<u4", "float32": "<f4", "float64": "<f8",
    }
    dtype_list = []
    in_v = False
    for l in lines:
        if l.startswith("element"):
            in_v = l.startswith("element vertex")
            continue
        if not in_v or not l.startswith("property") or l.startswith("property list"):
            continue
        parts = l.split()
        dtype_list.append((parts[2], NP.get(parts[1], "<f4")))

    dt = np.dtype(dtype_list)
    recs = np.frombuffer(raw, dtype=dt, count=n, offset=data_off)

    positions = np.stack([recs["x"], recs["y"], recs["z"]], axis=1).astype(np.float32)

    op = recs["opacity"].astype(np.float64)
    alphas = (255.0 / (1.0 + np.exp(-op))).clip(0, 255).astype(np.uint8)

    r = (0.5 + SH_C0 * recs["f_dc_0"]).clip(0, 1)
    g = (0.5 + SH_C0 * recs["f_dc_1"]).clip(0, 1)
    b = (0.5 + SH_C0 * recs["f_dc_2"]).clip(0, 1)
    colors = np.stack([r, g, b], axis=1).astype(np.float32)

    scales_log = np.stack([recs["scale_0"], recs["scale_1"], recs["scale_2"]], axis=1).astype(np.float32)

    qw = recs["rot_0"].astype(np.float64); qx = recs["rot_1"].astype(np.float64)
    qy = recs["rot_2"].astype(np.float64); qz = recs["rot_3"].astype(np.float64)
    ql = np.sqrt(qw**2 + qx**2 + qy**2 + qz**2).clip(1e-8)
    quats = np.stack([qw / ql, qx / ql, qy / ql, qz / ql], axis=1).astype(np.float32)

    return dict(n=n, sh_degree=0, frac_bits=0, flags=0,
                positions=positions, alphas=alphas, colors=colors,
                scales_log=scales_log, quats=quats)


if __name__ == "__main__":
    import sys
    s = parse_spz(sys.argv[1])
    p = s["positions"]
    op = s["alphas"] / 255.0
    solid = op > 0.3
    print(f"splats: {s['n']:,}   sh_degree={s['sh_degree']}")
    print(f"opaque (a>0.3): {solid.sum():,}")
    for ax, name in enumerate("XYZ"):
        lo, hi = p[:, ax].min(), p[:, ax].max()
        # robust extent on opaque splats
        rlo, rhi = np.percentile(p[solid, ax], [1, 99])
        print(f"  {name}: full [{lo:7.2f}, {hi:7.2f}]   1-99% [{rlo:7.2f}, {rhi:7.2f}]")
    c = p[solid].mean(axis=0)
    print(f"centroid(opaque): [{c[0]:.2f}, {c[1]:.2f}, {c[2]:.2f}]")
