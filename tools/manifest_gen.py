#!/usr/bin/env python3
"""
Build a content-addressed modpack release from a folder of Arma 2 OA mod directories.

Input layout (example):
    <modpack_root>/
        @Epoch/Addons/*.pbo
        @CBA_A2/Addons/*.pbo
        keys/*.bikey

Output (written to <backend_data_dir>):
    modpacks/<modpack_id>/modpack.json       # catalog entry
    modpacks/<modpack_id>/manifest.json      # {files: [{path, sha256, size}]}
    files/<aa>/<aa...>                       # content-addressed blobs, hard-linked

Usage:
    python manifest_gen.py \\
        --id epoch-1.0.7.1 \\
        --name "Epoch 1.0.7.1" \\
        --version 1.0.7.1 \\
        --source "C:/DayZ/Epoch" \\
        --out    "/opt/byes-launcher/data"
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
from pathlib import Path

CHUNK = 1 << 20  # 1 MiB


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        while chunk := f.read(CHUNK):
            h.update(chunk)
    return h.hexdigest()


def iter_files(root: Path):
    for p in root.rglob("*"):
        if p.is_file():
            yield p


def link_or_copy(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists():
        return
    try:
        os.link(src, dst)   # hard link — zero disk cost when same volume
    except OSError:
        shutil.copy2(src, dst)


def build(args: argparse.Namespace) -> None:
    source = Path(args.source).resolve()
    out = Path(args.out).resolve()
    if not source.is_dir():
        sys.exit(f"source not a directory: {source}")

    files_root = out / "files"
    pack_root = out / "modpacks" / args.id
    files_root.mkdir(parents=True, exist_ok=True)
    pack_root.mkdir(parents=True, exist_ok=True)

    manifest_files: list[dict] = []
    total_size = 0

    for f in sorted(iter_files(source)):
        rel = f.relative_to(source).as_posix().replace("/", "\\")  # Windows-style for the launcher
        h = sha256_of(f)
        size = f.stat().st_size
        total_size += size

        blob_path = files_root / h[:2] / h
        link_or_copy(f, blob_path)

        manifest_files.append({"path": rel, "sha256": h, "size": size})
        print(f"  {rel}  {h[:10]}…  {size:>10} B")

    manifest = {
        "modpackId": args.id,
        "version": args.version,
        "files": manifest_files,
    }
    (pack_root / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    # Top-level @Mod folders discovered in the manifest
    mods = sorted({entry["path"].split("\\", 1)[0] for entry in manifest_files if entry["path"].startswith("@")})

    catalog = {
        "id": args.id,
        "name": args.name,
        "version": args.version,
        "totalSize": total_size,
        "mods": mods,
        "description": args.description or "",
    }
    (pack_root / "modpack.json").write_text(json.dumps(catalog, indent=2), encoding="utf-8")

    print()
    print(f"modpack  : {args.id}")
    print(f"files    : {len(manifest_files)}")
    print(f"bytes    : {total_size:,}")
    print(f"mods     : {', '.join(mods) or '(none)'}")
    print(f"output   : {pack_root}")


def main() -> None:
    p = argparse.ArgumentParser(description="Build a BYES launcher modpack release.")
    p.add_argument("--id", required=True, help="URL-safe modpack id, e.g. epoch-1.0.7.1")
    p.add_argument("--name", required=True, help="Display name")
    p.add_argument("--version", required=True)
    p.add_argument("--source", required=True, help="Folder containing @ModName subfolders")
    p.add_argument("--out", required=True, help="Backend data dir (contains files/ and modpacks/)")
    p.add_argument("--description", default="")
    build(p.parse_args())


if __name__ == "__main__":
    main()
