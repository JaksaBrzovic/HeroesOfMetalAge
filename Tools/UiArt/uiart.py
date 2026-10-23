"""Shared helpers for cutting the gameplay UI out of the mockups: text erasure by inpainting, crops, 9-slice stitching."""
import cv2
import numpy as np
from PIL import Image

import os

# the selected mockups (GameplayUI/Selected from the art drop); override with the HOMA_UI_MOCKUPS environment variable
SRC = os.environ.get("HOMA_UI_MOCKUPS", os.path.expanduser("~/Downloads/HOMA_ART/GameplayUI/Selected")).rstrip("/\\") + "/"


def load(name):
    """RGBA numpy array (H, W, 4)."""
    return np.array(Image.open(SRC + name).convert("RGBA"))


def save(array, path):
    Image.fromarray(array).save(path)


def text_mask(rgb, rect, mode, threshold=38, dilate=3):
    """Pixels of text inside rect: brighter (mode 'light' text) or darker (mode 'dark' text) than the local background."""
    x0, y0, x1, y1 = rect
    region = rgb[y0:y1, x0:x1].astype(np.float32)
    lum = region @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    k = max(3, (min(x1 - x0, y1 - y0) // 2) | 1)
    k = min(k, 51)
    background = cv2.medianBlur(lum.astype(np.uint8), k).astype(np.float32)
    if mode == "both":
        diff = np.abs(lum - background)
    else:
        diff = lum - background if mode == "light" else background - lum
    mask = (diff > threshold).astype(np.uint8) * 255
    if dilate:
        mask = cv2.dilate(mask, np.ones((dilate * 2 + 1, dilate * 2 + 1), np.uint8))
    full = np.zeros(rgb.shape[:2], np.uint8)
    full[y0:y1, x0:x1] = mask
    return full


def erase(image, rects, mode="light", threshold=38, dilate=3, radius=5):
    """Inpaints the text in each rect (x0, y0, x1, y1). mode 'light' = light text on dark, 'dark' = dark text on light,
    'all' = the whole rect."""
    rgb = np.ascontiguousarray(image[:, :, :3])
    mask = np.zeros(rgb.shape[:2], np.uint8)
    for rect in rects:
        if mode == "all":
            x0, y0, x1, y1 = rect
            mask[y0:y1, x0:x1] = 255
        else:
            mask |= text_mask(rgb, rect, mode, threshold, dilate)
    bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
    out = cv2.inpaint(bgr, mask, radius, cv2.INPAINT_TELEA)
    image[:, :, :3] = cv2.cvtColor(out, cv2.COLOR_BGR2RGB)
    return image


def patch(image, source_rect, target_xy):
    """Copies a rectangle of the image onto another place of itself (to repeat a clean texture or an empty slot)."""
    x0, y0, x1, y1 = source_rect
    tx, ty = target_xy
    image[ty:ty + (y1 - y0), tx:tx + (x1 - x0)] = image[y0:y1, x0:x1].copy()
    return image


def feather_patch(image, source_rect, target_xy, feather=6):
    """Like patch, but blends the edges so the seam does not show."""
    x0, y0, x1, y1 = source_rect
    tx, ty = target_xy
    h, w = y1 - y0, x1 - x0
    src = image[y0:y1, x0:x1].astype(np.float32)
    dst = image[ty:ty + h, tx:tx + w].astype(np.float32)
    alpha = np.ones((h, w), np.float32)
    for i in range(feather):
        a = (i + 1) / (feather + 1)
        alpha[i, :] = np.minimum(alpha[i, :], a)
        alpha[h - 1 - i, :] = np.minimum(alpha[h - 1 - i, :], a)
        alpha[:, i] = np.minimum(alpha[:, i], a)
        alpha[:, w - 1 - i] = np.minimum(alpha[:, w - 1 - i], a)
    image[ty:ty + h, tx:tx + w] = (src * alpha[..., None] + dst * (1 - alpha[..., None])).astype(np.uint8)
    return image


def crop(image, rect):
    x0, y0, x1, y1 = rect
    return np.ascontiguousarray(image[y0:y1, x0:x1].copy())


def clear(image, rect):
    """Makes a rectangle transparent (a viewport the world shows through)."""
    x0, y0, x1, y1 = rect
    image[y0:y1, x0:x1, 3] = 0
    return image


def hstitch(image, left_rect, right_rect):
    """Joins the left part of one crop to the right part of another (same height): drops the middle of a frame or button
    so it can be 9-sliced without its text or ornament."""
    left = crop(image, left_rect)
    right = crop(image, right_rect)
    return np.concatenate([left, right], axis=1)


def vstitch(image, top_rect, bottom_rect):
    top = crop(image, top_rect)
    bottom = crop(image, bottom_rect)
    return np.concatenate([top, bottom], axis=0)


def resize(array, size):
    return np.array(Image.fromarray(array).resize(size, Image.LANCZOS))


def scale(array, factor):
    h, w = array.shape[:2]
    return resize(array, (max(1, round(w * factor)), max(1, round(h * factor))))


def plate(image, rect, top=30, bottom=12):
    """Fills a rectangle with a plain dark iron gradient (an empty slot or picture frame)."""
    x0, y0, x1, y1 = rect
    shade = np.linspace(top, bottom, y1 - y0)[:, None]
    noise = np.random.default_rng(3).normal(0, 2.0, (y1 - y0, x1 - x0))
    value = np.clip(shade + noise, 0, 255)
    image[y0:y1, x0:x1, 0] = value
    image[y0:y1, x0:x1, 1] = value * 0.92
    image[y0:y1, x0:x1, 2] = value * 0.85
    image[y0:y1, x0:x1, 3] = 255
    return image
