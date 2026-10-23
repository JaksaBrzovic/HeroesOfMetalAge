"""Shapes the derived terrain for the mission's design and picks the cells of every site.
The castle becomes solid but for its gate (the town); the portal's clearing gets a wall with one way in (where the Black
Banner stands); the chained maiden's pocket in the south-east gets a wall with one way in (the red tent), the minotaurs
inside before her. Every site must be reachable from the start. Writes map/final_terrain.json, map/sites.json and
map/sites_overlay.png."""
import heapq
import json
import os

import numpy as np
from PIL import Image, ImageDraw

import terrain as T

HERE = os.path.join(os.environ.get("TEMP", "/tmp"), "homa-map")
os.makedirs(os.path.join(HERE, "map"), exist_ok=True)
data = json.load(open(os.path.join(HERE, "map", "terrain.json")))
t = np.array(data["cells"])
rows, cols = t.shape
protected = set()


def box(x0, y0, x1, y1, value, keep=()):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if (x, y) not in keep:
                t[y, x] = value


def wall(x0, y0, x1, y1, gaps):
    """A ring of obstacles around a box, with gaps."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if (x in (x0, x1) or y in (y0, y1)) and (x, y) not in gaps:
                t[y, x] = T.OBSTACLE
                protected.add((x, y))


def walkable(x, y):
    return 0 <= x < cols and 0 <= y < rows and t[y, x] in T.WALKABLE


def reachable(start):
    seen = np.zeros_like(t, bool)
    stack = [start]
    seen[start[1], start[0]] = True
    while stack:
        x, y = stack.pop()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if walkable(nx, ny) and not seen[ny, nx]:
                seen[ny, nx] = True
                stack.append((nx, ny))
    return seen


def connect(cell, start):
    """Carves a corridor of rough ground from a cell to the reachable network, through the cheapest scenery."""
    seen = reachable(start)
    if seen[cell[1], cell[0]]:
        return
    cost_of = {T.OBSTACLE: 3, T.FOREST: 4, T.MOUNTAIN: 8}
    dist = {cell: 0}
    prev = {}
    heap = [(0, cell)]
    while heap:
        d, (x, y) = heapq.heappop(heap)
        if seen[y, x]:
            target = (x, y)
            break
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if not (0 <= nx < cols and 0 <= ny < rows) or (nx, ny) in protected or t[ny, nx] in (T.WATER, T.LAVA):
                continue
            step = 1 if walkable(nx, ny) else cost_of.get(t[ny, nx], 5)
            if d + step < dist.get((nx, ny), 1e9):
                dist[(nx, ny)] = d + step
                prev[(nx, ny)] = (x, y)
                heapq.heappush(heap, (d + step, (nx, ny)))
    node = target
    while node != cell:
        x, y = node
        if not walkable(x, y):
            t[y, x] = T.ROUGH if x > cols * 0.6 else T.ROAD
        node = prev[node]
    t[cell[1], cell[0]] = T.ROUGH if cell[0] > cols * 0.6 else T.ROAD


def open_cell(near, radius=4, taken=frozenset()):
    """The walkable cell near a point with the most walkable neighbours (the middle of a clearing)."""
    best = None
    for y in range(max(0, near[1] - radius), min(rows, near[1] + radius + 1)):
        for x in range(max(0, near[0] - radius), min(cols, near[0] + radius + 1)):
            if not walkable(x, y) or (x, y) in taken:
                continue
            score = sum(walkable(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)) * 10 - abs(x - near[0]) - abs(y - near[1])
            if best is None or score > best[0]:
                best = (score, (x, y))
    if best is None:
        raise RuntimeError("no walkable cell near %s" % (near,))
    return best[1]


START = T.START

# the castle: solid stone but for its gate, where the town stands
box(68, 14, 79, 26, T.OBSTACLE)
TOWN = (72, 27)
t[27, 72] = T.ROAD
t[28, 72] = T.ROAD

# the portal's clearing: a wall with one way in from the road, held by the Black Banner
box(86, 29, 90, 31, T.ROAD)
wall(85, 28, 91, 32, gaps={(85, 30)})
BOSS = (85, 30)
PORTAL = (89, 30)
t[30, 84] = T.ROAD
t[30, 85] = T.ROAD

# the maiden's pocket in the south-east: one way in from the west through the red tent, the minotaurs in front of her
box(86, 46, 91, 51, T.OBSTACLE)
# one row of ground inside, so nobody slips past the minotaurs
box(86, 48, 91, 48, T.ROUGH)
wall(85, 45, 92, 52, gaps={(85, 48)})
TENT = (85, 48)
t[48, 85] = T.ROUGH
MINOTAURS = (87, 48)
MAIDEN = (90, 48)
connect((84, 48), START)

sites = {
    "start": START,
    "town": TOWN,
    "boss": BOSS,
    "portal": PORTAL,
    "tent": TENT,
    "minotaurs": MINOTAURS,
    "maiden": MAIDEN,
}
# green side: the ten painted clearings and the roads between them (cells from the route prompt's positions)
wanted = {
    "willing": (7, 26),       # the Air Guitarists who join near the start
    "gold1": (12, 31),
    "colosseum": (9, 7),
    "mill": (31, 5),
    "gloves-guard": (45, 13),
    "gloves-cache": (47, 13),
    "chest": (21, 18),
    "horns": (37, 29),
    "bastion": (16, 37),
    "crew": (41, 42),
    "quest": (8, 51),
    "camp": (29, 54),
    "gold2": (24, 45),
    "bandits": (34, 37),
    "wardens": (55, 30),
    "buckler": (20, 27),
    "oracle": (90, 12),
    "revenants": (80, 40),
    "gold3": (78, 34),
    "ring": (70, 46),
}
taken = set(sites.values())
for name, near in wanted.items():
    cell = open_cell(near, 4, taken)
    connect(cell, START)
    sites[name] = cell
    taken.add(cell)

def choke(target, near, exclude):
    """The walkable cell nearest to near whose blocking cuts target off from the start: where a guard truly guards."""
    best = None
    for y in range(rows):
        for x in range(cols):
            if not walkable(x, y) or (x, y) in exclude or (x, y) == target:
                continue
            distance = abs(x - near[0]) + abs(y - near[1])
            if best is not None and distance >= best[0]:
                continue
            saved = t[y, x]
            t[y, x] = T.OBSTACLE
            cut = not reachable(START)[target[1], target[0]]
            t[y, x] = saved
            if cut:
                best = (distance, (x, y))
    return best[1]


taken = set(sites.values())
sites["wardens"] = choke(sites["town"], (58, 30), taken - {sites["wardens"]})
taken = set(sites.values())
sites["gloves-guard"] = choke(sites["gloves-cache"], sites["gloves-cache"], taken - {sites["gloves-guard"]})

seen = reachable(START)
for name, (x, y) in sites.items():
    assert seen[y, x], name + " is not reachable"
print("all sites reachable;", int(seen.sum()), "walkable cells")
json.dump({"width": cols, "height": rows, "cells": t.tolist()}, open(os.path.join(HERE, "map", "final_terrain.json"), "w"))
json.dump({k: list(v) for k, v in sites.items()}, open(os.path.join(HERE, "map", "sites.json"), "w"), indent=1)

image = Image.open(T.MAP_IMAGE).convert("RGB")
T.overlay(image, t, cols, rows)
over = Image.open(os.path.join(HERE, "map", "terrain_overlay.png"))
draw = ImageDraw.Draw(over)
for name, (x, y) in sites.items():
    draw.rectangle((x * 32, y * 32, x * 32 + 31, y * 32 + 31), outline=(255, 0, 0), width=3)
    draw.text((x * 32 + 2, y * 32 - 12), name, fill=(255, 255, 255))
over.save(os.path.join(HERE, "map", "sites_overlay.png"))
for k, v in sites.items():
    print(k, v)
