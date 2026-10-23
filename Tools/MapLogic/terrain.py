"""Derives the adventure map's terrain logic layer from the painted map image: every 64 px cell gets a terrain type from the
colours under it, short breaks in the painted paths are bridged, diagonal-only joins are widened (the rules allow no
diagonal step past a blocked corner) and walkable specks the hero can never reach are dropped. Writes map/terrain.json
(rows from the top) and map/terrain_overlay.png to check by eye."""
import heapq
import json
import os
import sys

import cv2
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.join(os.environ.get("TEMP", "/tmp"), "homa-map")
os.makedirs(os.path.join(HERE, "map"), exist_ok=True)
CELL = 64
# the painted map in the project; work files and overlays go to %TEMP%/homa-map/map
MAP_IMAGE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "HeroesOfMetalAge", "Content", "Maps", "AshenPass", "AshenPass.png")
SUB = 16  # analysis pixels per cell side (image downscaled by 4)
PATH_THRESHOLD = 0.16
MAX_GAP = 6
START = (3, 30)

# TerrainType values (Domain): Grass 0, Forest 1, Road 2, Mountain 3, Water 4, Blocked 5, Dirt 6, Rough 7, Sand 8, Swamp 9, Lava 10, Obstacle 11
GRASS, FOREST, ROAD, MOUNTAIN, WATER, BLOCKED, DIRT, ROUGH, SAND, SWAMP, LAVA, OBSTACLE = range(12)
WALKABLE = (GRASS, ROAD, DIRT, ROUGH, SAND, SWAMP)
COLOURS = {GRASS: (120, 200, 60), FOREST: (20, 90, 30), ROAD: (240, 200, 120), MOUNTAIN: (150, 150, 150), WATER: (40, 120, 230),
           DIRT: (200, 140, 80), ROUGH: (150, 100, 70), LAVA: (255, 60, 0), OBSTACLE: (90, 40, 90), BLOCKED: (0, 0, 0)}


def classify_pixels(rgb):
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV_FULL).astype(np.float32)
    h = hsv[:, :, 0] * 360 / 256
    s = hsv[:, :, 1] / 255
    v = hsv[:, :, 2] / 255
    r, g, b = [rgb[:, :, i].astype(np.float32) for i in range(3)]
    return dict(
        path=(h >= 18) & (h <= 45) & (s >= 0.38) & (s <= 0.78) & (v >= 0.55),
        dark_road=(h >= 18) & (h <= 45) & (s >= 0.45) & (v >= 0.4),
        ground=(h >= 8) & (h <= 48) & (s >= 0.25) & (s <= 0.65) & (v >= 0.24) & (v < 0.62),
        stone=(s < 0.3) & (v >= 0.25),
        grass=(h > 45) & (h <= 110) & (s >= 0.3) & (v >= 0.42),
        tree=((h > 60) & (h <= 170) & (v < 0.42)) | ((h > 45) & (h <= 170) & (s > 0.55) & (v < 0.5)),
        water=(h > 170) & (h < 250) & (s > 0.2) & (v > 0.2) & (b > r + 10),
        lava=(h < 35) & (s > 0.65) & (v > 0.7) & (r > 200),
    )


def cell_fractions(mask, rows, cols):
    return mask[:rows * SUB, :cols * SUB].reshape(rows, SUB, cols, SUB).mean(axis=(1, 3))


def classify(f, dark):
    rows, cols = dark.shape
    terrain = np.full((rows, cols), FOREST, np.int32)
    for y in range(rows):
        for x in range(cols):
            p, gr, tr, wa, la = (f[k][y, x] for k in ("path", "grass", "tree", "water", "lava"))
            dk, gd, st = f["dark_road"][y, x], f["ground"][y, x], f["stone"][y, x]
            if dark[y, x]:
                if la > 0.06:
                    terrain[y, x] = LAVA
                elif dk > 0.35:
                    terrain[y, x] = ROAD
                elif st > 0.45:
                    terrain[y, x] = MOUNTAIN
                elif gd > 0.45 and st < 0.3:
                    terrain[y, x] = ROUGH
                else:
                    terrain[y, x] = OBSTACLE
            elif p > PATH_THRESHOLD:
                terrain[y, x] = ROAD
            elif wa > 0.3:
                terrain[y, x] = WATER
            elif st > 0.35:
                terrain[y, x] = MOUNTAIN
            elif gr > 0.42 and tr < 0.25:
                terrain[y, x] = GRASS
            else:
                terrain[y, x] = FOREST
    return terrain


def components(terrain):
    walk = np.isin(terrain, WALKABLE).astype(np.uint8)
    count, labels = cv2.connectedComponents(walk, connectivity=4)
    return count, labels


def bridge(terrain, f, dark):
    """Joins every walkable piece of at least a few cells to the network over at most MAX_GAP cells, along the cells that
    look most like path (the painted path is there, only too thin or shaded to be recognised)."""
    rows, cols = terrain.shape
    joined = 0
    while True:
        count, labels = components(terrain)
        sizes = np.bincount(labels.ravel())
        sizes[0] = 0
        main = labels[START[1], START[0]] if terrain[START[1], START[0]] in WALKABLE else int(sizes.argmax())
        best = None
        # Dijkstra from the main network over non-walkable cells; reaching another piece within MAX_GAP joins it
        dist = np.full((rows, cols), np.inf)
        prev = {}
        heap = []
        for y, x in zip(*np.where(labels == main)):
            dist[y, x] = 0
            heapq.heappush(heap, (0.0, 0, x, y))
        while heap:
            d, steps, x, y = heapq.heappop(heap)
            if d > dist[y, x]:
                continue
            if labels[y, x] not in (0, main) and sizes[labels[y, x]] >= 3:
                best = (x, y)
                break
            if steps >= MAX_GAP and labels[y, x] == 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < cols and 0 <= ny < rows) or labels[ny, nx] == main:
                    continue
                if labels[ny, nx] == 0:
                    if terrain[ny, nx] in (LAVA, MOUNTAIN) or (terrain[ny, nx] == WATER and f["path"][ny, nx] < 0.04):
                        continue
                    likeness = f["dark_road"][ny, nx] + f["ground"][ny, nx] if dark[ny, nx] else f["path"][ny, nx] + 0.5 * f["grass"][ny, nx]
                    cost = 1.0 + 6.0 * max(0.0, 1.0 - 4.0 * likeness)
                    nsteps = steps + 1
                else:
                    cost = 0.0
                    nsteps = steps
                if d + cost < dist[ny, nx]:
                    dist[ny, nx] = d + cost
                    prev[(nx, ny)] = (x, y)
                    heapq.heappush(heap, (d + cost, nsteps, nx, ny))
        if best is None:
            break
        cell = best
        while cell in prev:
            x, y = cell
            if labels[y, x] == 0:
                terrain[y, x] = ROUGH if dark[y, x] else ROAD
            cell = prev[cell]
        joined += 1
    print("pieces joined:", joined)


def walkable(terrain, x, y):
    rows, cols = terrain.shape
    return 0 <= x < cols and 0 <= y < rows and terrain[y, x] in WALKABLE


def widen_diagonals(terrain, path):
    """A path that only touches diagonally gets one of the two corner cells (the one more like a path) as road."""
    rows, cols = terrain.shape
    changed = True
    while changed:
        changed = False
        for y in range(rows - 1):
            for x in range(cols):
                for dx in (-1, 1):
                    nx, ny = x + dx, y + 1
                    if not (0 <= nx < cols) or not (walkable(terrain, x, y) and walkable(terrain, nx, ny)):
                        continue
                    if walkable(terrain, nx, y) or walkable(terrain, x, ny):
                        continue
                    a, b = (nx, y), (x, ny)
                    pick = a if path[a[1], a[0]] >= path[b[1], b[0]] else b
                    if terrain[pick[1], pick[0]] in (WATER, LAVA):
                        pick = b if pick == a else a
                    terrain[pick[1], pick[0]] = ROUGH if terrain[y, x] == ROUGH else ROAD
                    changed = True


def keep_reachable(terrain, start):
    """Walkable cells the hero can never reach become what surrounds them."""
    rows, cols = terrain.shape
    count, labels = components(terrain)
    main = labels[start[1], start[0]]
    for y in range(rows):
        for x in range(cols):
            if terrain[y, x] in WALKABLE and labels[y, x] != main:
                terrain[y, x] = OBSTACLE if x > cols * 0.6 else FOREST
    print("reachable cells:", int((labels == main).sum()))


def main(skip_cleanup=False):
    image = Image.open(MAP_IMAGE).convert("RGB")
    cols, rows = image.width // CELL, image.height // CELL
    small = np.array(image.resize((cols * SUB, rows * SUB), Image.BOX))
    f = {name: cell_fractions(mask, rows, cols) for name, mask in classify_pixels(small).items()}
    hsv = cv2.cvtColor(small, cv2.COLOR_RGB2HSV_FULL).astype(np.float32)
    hue = hsv[:, :, 0] * 360 / 256
    green = cell_fractions((hue > 50) & (hue < 170) & (hsv[:, :, 1] > 70), rows, cols)
    # the dark lands lie east of the dividing mountains: little green and to the right of the map's middle
    dark = (cv2.blur(green.astype(np.float32), (5, 5)) < 0.12) & (np.arange(cols)[None, :] > cols * 0.55)
    terrain = classify(f, dark)
    if not skip_cleanup:
        bridge(terrain, f, dark)
        widen_diagonals(terrain, f["path"])
        keep_reachable(terrain, START)
    with open(os.path.join(HERE, "map", "terrain.json"), "w") as out:
        json.dump({"width": cols, "height": rows, "cells": terrain.tolist()}, out)
    overlay(image, terrain, cols, rows)
    print(cols, rows, {k: int((terrain == k).sum()) for k in COLOURS})


def overlay(image, terrain, cols, rows):
    view = np.array(image.resize((cols * 16, rows * 16), Image.BOX)).astype(np.float32)
    tint = np.zeros_like(view)
    for k, c in COLOURS.items():
        tint[np.kron(terrain == k, np.ones((16, 16), bool))] = c
    out = Image.fromarray((view * 0.45 + tint * 0.55).astype(np.uint8)).resize((cols * 32, rows * 32), Image.NEAREST)
    from PIL import ImageDraw
    draw = ImageDraw.Draw(out)
    for x in range(0, cols, 5):
        draw.line([(x * 32, 0), (x * 32, out.height)], fill=(255, 255, 255))
        draw.text((x * 32 + 2, 2), str(x), fill=(255, 255, 0))
    for y in range(0, rows, 5):
        draw.line([(0, y * 32), (out.width, y * 32)], fill=(255, 255, 255))
        draw.text((2, y * 32 + 2), str(y), fill=(255, 255, 0))
    out.save(os.path.join(HERE, "map", "terrain_overlay.png"))


if __name__ == "__main__":
    main("--raw" in sys.argv)
