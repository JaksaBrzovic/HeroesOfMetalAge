"""The Gloves of Metal icon: the gauntlet silhouette of the hero mockup's empty GLOVES slot, cut out and forged into a
legendary obsidian-and-gold piece with an ember glow, on the item icons' transparent 512 x 512 canvas."""
import cv2
import numpy as np
from PIL import Image, ImageFilter

from uiart import SRC as MOCKUPS
import os

SRC = MOCKUPS + "04-hero-screen.png"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "HeroesOfMetalAge", "Content", "UI", "Sprites", "Artefacts", "legendary-gloves.png")

image = np.array(Image.open(SRC).convert("RGB"))[290:370, 600:684].astype(np.float32)
lum = image @ np.array([0.299, 0.587, 0.114], np.float32)
background = np.percentile(lum, 30)
alpha = np.clip((lum - background - 12) / 22, 0, 1)
mask = (alpha > 0.5).astype(np.uint8)
count, labels, stats, _ = cv2.connectedComponentsWithStats(mask)
largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
alpha *= cv2.dilate((labels == largest).astype(np.uint8), np.ones((5, 5), np.uint8))
alpha = cv2.GaussianBlur(alpha, (3, 3), 0)

size = 400
lum_big = cv2.resize(lum, (size, int(size * lum.shape[0] / lum.shape[1])), interpolation=cv2.INTER_CUBIC)
alpha_big = cv2.resize(alpha, lum_big.shape[::-1], interpolation=cv2.INTER_CUBIC)
shade = np.clip((lum_big - lum_big[alpha_big > 0.5].min()) / max(1, np.ptp(lum_big[alpha_big > 0.5])), 0, 1)
obsidian = np.array([30, 22, 26], np.float32)
gold_dark = np.array([150, 92, 30], np.float32)
gold_light = np.array([255, 222, 140], np.float32)
low = np.clip(shade * 2, 0, 1)[..., None]
high = np.clip(shade * 2 - 1, 0, 1)[..., None]
colour = obsidian * (1 - low) + gold_dark * low
colour = colour * (1 - high) + gold_light * high
# engraved edges glint brighter
edges = cv2.Laplacian(cv2.GaussianBlur(lum_big, (3, 3), 0), cv2.CV_32F)
colour += np.clip(edges, 0, 40)[..., None] * np.array([1.4, 1.1, 0.6])
colour = np.clip(colour, 0, 255)

canvas = np.zeros((512, 512, 4), np.float32)
h, w = alpha_big.shape
oy, ox = (512 - h) // 2, (512 - w) // 2
glow = cv2.GaussianBlur(cv2.dilate(alpha_big, np.ones((15, 15), np.uint8)), (0, 0), 14)
canvas[oy:oy + h, ox:ox + w, :3] = np.array([255, 120, 30], np.float32)
canvas[oy:oy + h, ox:ox + w, 3] = glow * 0.75
piece = np.dstack([colour, alpha_big])
a = piece[:, :, 3:4]
region = canvas[oy:oy + h, ox:ox + w]
region[:, :, :3] = piece[:, :, :3] * a + region[:, :, :3] * (1 - a)
region[:, :, 3:4] = a + region[:, :, 3:4] * (1 - a)
canvas[:, :, 3] *= 255
Image.fromarray(np.clip(canvas, 0, 255).astype(np.uint8)).save(OUT)
print("written", OUT)
