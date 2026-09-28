# -*- coding: utf-8 -*-
"""
부대 관계도 스냅숏 측정(TEST-36) — Pillow.

    python measure_units_map.py <스냅숏 폴더>

미리보기 `--units-map --snapshot <폴더>` 가 남긴 PNG 와 곁 파일(JSON: 노드 사각형 · 상태 · 토큰 색)을 읽어 잰다.
  ① 선택 막대 3px(L2 고른 카드 왼쪽)  ② 후보 윤곽이 파선(끊김이 있다)  ③ 막힘 해치(줄무늬)
  ④ 배지 글자 대비 ≥ 4.5:1  ⑤ 흑백 변환에서 여섯 상태 구분(선택 · 후보 · 막힘 · 머묾 · 끌리는 사본 · 원래 자리 — NFR-06)
  ⑥ 반 픽셀 뭉개짐 없음 — 틀 세로 변이 한 열에 몰린다(NFR-08)
토큰 색은 WPF #AARRGGBB 를 그대로 읽는다(CSS 순서로 바꾸지 않는다 — 메모리 storyboard_argb_css_hex_order).
결과: 표준 출력 + <폴더>/measure.json
"""
import json
import math
import os
import sys

from PIL import Image


def argb(text):
    """WPF '#AARRGGBB' → (r, g, b)."""
    text = text.lstrip('#')
    if len(text) == 8:
        return tuple(int(text[i:i + 2], 16) for i in (2, 4, 6))
    return tuple(int(text[i:i + 2], 16) for i in (0, 2, 4))


def lum(rgb):
    def ch(c):
        c = c / 255.0
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = rgb[:3]
    return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b)


def contrast(a, b):
    la, lb = sorted((lum(a), lum(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def close(a, b, tol=40):
    return all(abs(x - y) <= tol for x, y in zip(a[:3], b[:3]))


def load(folder, name):
    image = Image.open(os.path.join(folder, name + '.png')).convert('RGB')
    with open(os.path.join(folder, name + '.json'), encoding='utf-8') as f:
        meta = json.load(f)
    return image, meta


def box(rect):
    x, y, w, h = rect
    return int(round(x)), int(round(y)), int(round(x + w)), int(round(y + h))


def selection_bar_width(image, meta):
    sel = argb(meta['tokens']['SelectionBrush'])
    for node in meta['nodes']:
        if node['selected'] and node['level'] == 'L2':
            x0, y0, x1, y1 = box(node['rect'])
            y = (y0 + y1) // 2
            run = 0
            for x in range(x0, x0 + 8):
                if close(image.getpixel((x, y)), sel, 60):
                    run += 1
                elif run:
                    break
            return run
    return None


def dash_transitions(image, meta, state):
    pri = argb(meta['tokens']['PrimaryBrush'])
    for node in meta['nodes']:
        if node['state'] == state:
            x0, y0, x1, y1 = box(node['rect'])
            y = y0 - 4                                   # 윤곽은 노드 둘레 4 DIU 밖(Margin -4)
            if y < 0:
                continue
            on = [close(image.getpixel((x, y)), pri, 70) for x in range(x0 + 6, x1 - 6)]
            flips = sum(1 for a, b in zip(on, on[1:]) if a != b)
            if any(on):
                return flips, node['name']
    return None, None


def hatch_stripes(image, meta):
    for node in meta['nodes']:
        if node['state'] == 'Blocked' and node['level'] in ('L1', 'L2'):
            fx0, fy0, fx1, fy1 = box(node['frame'])
            gray = image.crop((fx0 + 2, fy0 + 2, fx1 - 2, fy1 - 2)).convert('L')
            row = [gray.getpixel((x, gray.height // 2)) for x in range(gray.width)]
            mean = sum(row) / len(row)
            off = [abs(v - mean) > 10 for v in row]            # 선은 바탕보다 밝을 수도(다크) 어두울 수도(라이트) 있다
            flips = sum(1 for a, b in zip(off, off[1:]) if a != b)
            return flips, node['name']
    return None, None


def badge_contrast(image, meta):
    """L2 카드의 '장비 N' 배지 줄(카드 안 x 54~110, y 38~52) — 가장 어두운 글자 대 바탕."""
    best = None
    for node in meta['nodes']:
        if node['level'] != 'L2':
            continue
        x0, y0, _, _ = box(node['rect'])
        region = image.crop((x0 + 56, y0 + 39, x0 + 100, y0 + 51))
        pixels = list(region.get_flattened_data()) if hasattr(region, "get_flattened_data") else list(region.getdata())
        if not pixels:
            continue
        background = max(pixels, key=lum) if lum(pixels[0]) > 0.5 else min(pixels, key=lum)
        text = min(pixels, key=lum) if lum(background) > 0.5 else max(pixels, key=lum)
        ratio = contrast(text, background)
        best = ratio if best is None else min(best, ratio)
    return best


def features(image, rect):
    x0, y0, x1, y1 = box(rect)
    gray = image.crop((max(0, x0 - 6), max(0, y0 - 6), x1 + 6, y1 + 6)).convert('L')
    data = list(gray.get_flattened_data()) if hasattr(gray, "get_flattened_data") else list(gray.getdata())
    mean = sum(data) / len(data)
    std = math.sqrt(sum((d - mean) ** 2 for d in data) / len(data))
    w = gray.width
    edges = sum(1 for i in range(len(data) - 1) if (i + 1) % w and abs(data[i] - data[i + 1]) > 40) / len(data)
    return mean / 255.0, std / 128.0, edges * 4


def six_states(folder, theme):
    drag, drag_meta = load(folder, f'umap-{theme}-04-drag-parent')
    sel_img, sel_meta = load(folder, f'umap-{theme}-02-L1-50')
    picks = {}
    for node in drag_meta['nodes']:
        state = node['state']
        if node['level'] == 'L1' and state in ('ParentCandidate', 'Blocked', 'Origin', 'Hover') and state not in picks:
            picks[state] = features(drag, node['rect'])
    # 끌리는 사본은 포인터 아래(머문 노드 위)에 뜨므로 빈 곳 위 장면(07)에서 잰다.
    position, position_meta = load(folder, f'umap-{theme}-07-drag-position')
    if position_meta.get('ghost'):
        picks['Ghost'] = features(position, position_meta['ghost'])
    for node in sel_meta['nodes']:
        if node['selected']:
            picks['Selected'] = features(sel_img, node['rect'])
    names = sorted(picks)
    worst = None
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            d = math.dist(picks[a], picks[b])
            if worst is None or d < worst[0]:
                worst = (round(d, 3), a, b)
    return names, worst


def frame_sharpness(image, meta):
    """틀 왼쪽 세로 변 둘레 5열의 어두움 몫 — 한 열에 몰릴수록 선명(반 픽셀 번짐이면 두 열로 갈린다)."""
    ratios = []
    for node in meta['nodes'][:40]:
        if node['level'] != 'L2':
            continue
        fx0, fy0, _, fy1 = box(node['frame'])
        y = (fy0 + fy1) // 2
        gray = image.convert('L')
        background = gray.getpixel((fx0 - 5, y))
        cols = [abs(gray.getpixel((x, y)) - background) for x in range(fx0 - 2, fx0 + 3)]
        total = sum(cols) or 1
        ratios.append(max(cols) / total)
    return round(sum(ratios) / len(ratios), 3) if ratios else None


def main(folder):
    report = {}
    for theme in ('light', 'dark'):
        l2, l2_meta = load(folder, f'umap-{theme}-03-L2-100')
        drag, drag_meta = load(folder, f'umap-{theme}-04-drag-parent')
        adjoin, adjoin_meta = load(folder, f'umap-{theme}-05-drag-adjoin')
        flips, candidate = dash_transitions(drag, drag_meta, 'ParentCandidate')
        adj_flips, adj_candidate = dash_transitions(adjoin, adjoin_meta, 'AdjoinCandidate')
        hatch, hatched = hatch_stripes(drag, drag_meta)
        names, worst = six_states(folder, theme)
        report[theme] = {
            'selectionBarPx': selection_bar_width(l2, l2_meta),
            'candidateDashFlips': flips, 'candidateNode': candidate,
            'adjoinDashFlips': adj_flips, 'adjoinNode': adj_candidate,
            'blockedHatchFlips': hatch, 'blockedNode': hatched,
            'badgeContrastMin': round(badge_contrast(l2, l2_meta) or 0, 2),
            'grayscaleStates': names, 'grayscaleClosestPair': worst,
            'frameEdgeSharpness': frame_sharpness(l2, l2_meta),
        }
    with open(os.path.join(folder, 'measure.json'), 'w', encoding='utf-8') as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main(sys.argv[1])
