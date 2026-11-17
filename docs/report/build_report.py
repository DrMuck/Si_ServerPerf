"""Build docs/Si_ServerPerf_Report.pdf from report_source.txt and the CSV files in data/.

Usage:  python build_report.py [--out PATH]

Pipeline adapted from Silica_Performance/tools/build_dev_report.py (Markdown subset -> reportlab Platypus,
matplotlib charts). Extensions used by report_source.txt:
  !fig <name> <width_mm> | <caption>    insert chart <name> (drawn from data/, saved to charts/<name>.png)
  ::cap <text>                          table caption, kept on the page of the table that follows
  ---pagebreak---                       page break
  table rows whose first cell is bold and all other cells empty become full-width group rows.
Every number in the charts comes from the CSV files in data/ (each row names its source file).
"""
import argparse
import csv
import io
import os
import re
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.lines import Line2D
from matplotlib.patches import Patch

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT, TA_RIGHT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas as rl_canvas
from reportlab.platypus import (CondPageBreak, Image, KeepTogether, PageBreak,
                                Paragraph, SimpleDocTemplate, Spacer, Table,
                                TableStyle)
from reportlab.platypus.flowables import HRFlowable

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
CHARTS = os.path.join(HERE, "charts")
SOURCE = os.path.join(HERE, "report_source.txt")
DEFAULT_OUT = os.path.join(os.path.dirname(HERE), "Si_ServerPerf_Report.pdf")
FONTDIR = r"C:\Windows\Fonts"
FOOTER_LEFT = "Si_ServerPerf 0.2.0: server performance report"
TITLE = "Si_ServerPerf 0.2.0"
SUBTITLE = "What the server performance mod does, and how much faster the Silica dedicated server runs with it"
AUTHOR = "DrMuck"
DATE = "17 November 2026"


# ----------------------------------------------------------------- fonts ---
def register_fonts():
    def have(*names):
        return all(os.path.exists(os.path.join(FONTDIR, n)) for n in names)
    body = {"": "DejaVuSans.ttf", "B": "DejaVuSans-Bold.ttf",
            "I": "DejaVuSans-Oblique.ttf", "BI": "DejaVuSans-BoldOblique.ttf"}
    cond = {"": "DejaVuSansCondensed.ttf", "B": "DejaVuSansCondensed-Bold.ttf",
            "I": "DejaVuSansCondensed-Oblique.ttf",
            "BI": "DejaVuSansCondensed-BoldOblique.ttf"}
    mono = {"": "DejaVuSansMono.ttf", "B": "DejaVuSansMono-Bold.ttf",
            "I": "DejaVuSansMono-Oblique.ttf", "BI": "DejaVuSansMono-BoldOblique.ttf"}
    if not have(*body.values()):
        body = {"": "arial.ttf", "B": "arialbd.ttf", "I": "ariali.ttf", "BI": "arialbi.ttf"}
    if not have(*cond.values()):
        cond = body
    if not have(*mono.values()):
        mono = {"": "consola.ttf", "B": "consolab.ttf", "I": "consolai.ttf", "BI": "consolaz.ttf"}
    for fam, files in (("Body", body), ("Cond", cond), ("Mono", mono)):
        for k, f in files.items():
            pdfmetrics.registerFont(TTFont(fam + k, os.path.join(FONTDIR, f)))
        pdfmetrics.registerFontFamily(fam, normal=fam, bold=fam + "B",
                                      italic=fam + "I", boldItalic=fam + "BI")


# ---------------------------------------------------------------- styles ---
INK = colors.HexColor("#0b0b0b")
INK2 = colors.HexColor("#52514e")
ACCENT = colors.HexColor("#1f4e79")
GRID = colors.HexColor("#cdd2d9")
HEAD_BG = colors.HexColor("#e6ecf3")
GROUP_BG = colors.HexColor("#d9e2ec")
ALT_BG = colors.HexColor("#f5f7fa")

BODY_SIZE = 8.9
TABLE_SIZE = 7.4
CODE_SCALE = 0.88
PAGE_W, PAGE_H = A4
MARGIN = 17 * mm
AVAIL_W = PAGE_W - 2 * MARGIN


def make_styles():
    s = {}
    s["body"] = ParagraphStyle("body", fontName="Body", fontSize=BODY_SIZE,
                               leading=BODY_SIZE * 1.38, textColor=INK, spaceAfter=5)
    s["h2"] = ParagraphStyle("h2", parent=s["body"], fontName="BodyB", fontSize=14,
                             leading=18, textColor=ACCENT, spaceBefore=6, spaceAfter=6)
    s["h3"] = ParagraphStyle("h3", parent=s["body"], fontName="BodyB", fontSize=10.5,
                             leading=14, textColor=ACCENT, spaceBefore=7, spaceAfter=4)
    s["list"] = ParagraphStyle("list", parent=s["body"], spaceAfter=3, leftIndent=13,
                               bulletIndent=3)
    s["list2"] = ParagraphStyle("list2", parent=s["list"], leftIndent=26, bulletIndent=15,
                                spaceAfter=2)
    s["caption"] = ParagraphStyle("caption", parent=s["body"], fontName="BodyI", fontSize=7.9,
                                  leading=10.4, textColor=INK2, spaceBefore=2, spaceAfter=9)
    s["tcap"] = ParagraphStyle("tcap", parent=s["caption"], fontName="BodyB", textColor=INK,
                               spaceBefore=4, spaceAfter=3)
    for al, name in ((TA_LEFT, "L"), (TA_RIGHT, "R"), (TA_CENTER, "C")):
        s["cell" + name] = ParagraphStyle("cell" + name, fontName="Cond", fontSize=TABLE_SIZE,
                                          leading=TABLE_SIZE * 1.27, textColor=INK, alignment=al)
        s["head" + name] = ParagraphStyle("head" + name, parent=s["cell" + name], fontName="CondB")
    s["t_title"] = ParagraphStyle("t_title", parent=s["body"], fontName="BodyB", fontSize=30,
                                  leading=36, textColor=ACCENT, spaceAfter=10)
    s["t_sub"] = ParagraphStyle("t_sub", parent=s["body"], fontSize=14, leading=19,
                                textColor=INK, spaceAfter=18)
    s["t_meta"] = ParagraphStyle("t_meta", parent=s["body"], fontSize=10, leading=15,
                                 textColor=INK2, spaceAfter=2)
    s["t_box"] = ParagraphStyle("t_box", parent=s["body"], fontSize=9.6, leading=14.2,
                                spaceAfter=6)
    return s


# ------------------------------------------------------- inline markdown ---
NB = "\u00a0"
DIGIT_GROUP = re.compile(r"(?<![\d.,])(\d{1,3}) (?=\d{3}(?![\d.,]))")
UNIT_SPACE = re.compile(r"(?<=\d) (?=(%|ms|fps|s|µs|cm|mm|m|Hz)\b)")
PH_CODE = 0xE100


def protect_numbers(text):
    text = DIGIT_GROUP.sub(lambda m: m.group(1) + NB, text)
    return UNIT_SPACE.sub(NB, text)


def xml(t):
    return t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def inline(md, size):
    codes = []

    def keep_code(m):
        codes.append(m.group(1))
        return chr(PH_CODE + len(codes) - 1)
    t = re.sub(r"`([^`]+)`", keep_code, md)
    t = protect_numbers(xml(t))
    t = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", t)
    t = re.sub(r"(?<![*\w])\*(?=\S)(.+?)(?<=\S)\*(?![*\w])", r"<i>\1</i>", t)
    csize = round(size * CODE_SCALE, 2)
    for i, c in enumerate(codes):
        t = t.replace(chr(PH_CODE + i), '<font face="Mono" size="%s">%s</font>' % (csize, xml(c)))
    return t


def plain(md):
    t = md.replace("**", "").replace("`", "")
    return protect_numbers(t)


def text_width(md, bold, size, font="Cond"):
    w = 0.0
    for i, part in enumerate(md.split("`")):
        if i % 2:
            w += pdfmetrics.stringWidth(part, "Mono", size * CODE_SCALE)
        else:
            w += pdfmetrics.stringWidth(plain(part), font + ("B" if bold else ""), size)
    return w


def min_token_width(md, bold, size):
    best = 0.0
    for i, part in enumerate(md.split("`")):
        if i % 2:   # code spans do not break
            best = max(best, pdfmetrics.stringWidth(part, "Mono", size * CODE_SCALE))
        else:
            for tk in re.split(r"[ \t]+", protect_numbers(plain(part))):
                if tk:
                    best = max(best, pdfmetrics.stringWidth(tk, "Cond" + ("B" if bold else ""), size))
    return best


# ------------------------------------------------------- block parsing ----
LIST_RE = re.compile(r"^(\s*)([*+-]|\d+[.)])\s+(.*)$")
ALIGN_RE = re.compile(r"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$")


def split_row(line):
    line = line.strip()
    if line.startswith("|"):
        line = line[1:]
    if line.endswith("|"):
        line = line[:-1]
    return [c.strip() for c in line.split("|")]


def parse_blocks(md):
    lines = md.splitlines()
    blocks, i = [], 0
    while i < len(lines):
        ln = lines[i]
        s = ln.strip()
        if not s:
            i += 1
            continue
        if s == "---pagebreak---":
            blocks.append(("pb",))
            i += 1
            continue
        m = re.match(r"^(#{1,6})\s+(.*)$", s)
        if m:
            blocks.append(("h", len(m.group(1)), m.group(2).strip()))
            i += 1
            continue
        m = re.match(r"^!fig\s+(\w+)\s+(\d+)\s*\|\s*(.*)$", s)
        if m:
            blocks.append(("fig", m.group(1), float(m.group(2)), m.group(3)))
            i += 1
            continue
        if s.startswith("::cap "):
            blocks.append(("cap", s[6:]))
            i += 1
            continue
        if s.startswith("|") and i + 1 < len(lines) and ALIGN_RE.match(lines[i + 1].strip()):
            header = split_row(s)
            aligns = []
            for spec in split_row(lines[i + 1]):
                aligns.append("R" if spec.endswith(":") and not spec.startswith(":") else
                              "C" if spec.startswith(":") and spec.endswith(":") else "L")
            n = len(header)
            aligns = (aligns + ["L"] * n)[:n]
            rows = []
            i += 2
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append((split_row(lines[i]) + [""] * n)[:n])
                i += 1
            blocks.append(("table", header, aligns, rows))
            continue
        lm = LIST_RE.match(ln)
        if lm:
            items = []
            while i < len(lines):
                cur = lines[i]
                m2 = LIST_RE.match(cur)
                if m2:
                    items.append([1 if len(m2.group(1)) >= 2 else 0, m2.group(3).strip()])
                    i += 1
                elif cur.strip() and cur.startswith((" ", "\t")) and items:
                    items[-1][1] += " " + cur.strip()
                    i += 1
                else:
                    break
            blocks.append(("ul", items))
            continue
        para = [s]
        i += 1
        while i < len(lines):
            nx = lines[i].strip()
            if (not nx or nx.startswith(("#", "|", "!fig", "::cap", "---pagebreak"))
                    or LIST_RE.match(lines[i])):
                break
            para.append(nx)
            i += 1
        blocks.append(("p", para))
    return blocks


# ------------------------------------------------------------- charts -----
TXT, TXT2, MUTED = "#0b0b0b", "#52514e", "#898781"
GRIDC, AXIS = "#e1e0d9", "#c3c2b7"
# One colour, marker and line style per configuration, the same in every chart.
COL = {"off": "#7d7b75", "prev": "#2a78d6", "new": "#1baf7a", "max": "#eb6834",
       "new2": "#e87ba4", "all": "#6250d6", "agg": "#eda100", "parked": "#008300"}
MARK = {"off": "o", "prev": "s", "new": "^", "max": "D", "new2": "v", "all": "P", "agg": "X",
        "parked": "h"}
LS = {"off": (0, (1, 1.4)), "prev": (0, (5, 2)), "new": "-", "max": "-", "new2": (0, (3, 1, 1, 1)),
      "all": (0, (8, 2)), "agg": (0, (2, 2)), "parked": "-"}
LABEL = {"off": "Mod off", "prev": "Previous defaults (b10)", "new": "+ new invisible items",
         "max": "+ 5 s wreck removal (≈ 0.2.0 defaults)", "new2": "+ prefiltered replay",
         "all": "+ worker-thread scans (opt-in)", "agg": "Previous defaults + approximate set",
         "parked": "+ parked-vehicle set"}
SHORT = {"off": "Mod\noff", "prev": "Previous\ndefaults", "new": "+ new\nitems",
         "max": "+ 5 s\nremoval", "new2": "+ pre-\nfiltered", "all": "+ worker\nscans",
         "agg": "+ approx.\nset"}


def chart_style():
    plt.rcParams.update({
        "font.family": "DejaVu Sans", "font.size": 7.2,
        "axes.edgecolor": AXIS, "axes.linewidth": 0.7,
        "axes.labelcolor": TXT2, "xtick.color": TXT2, "ytick.color": TXT2,
        "xtick.major.width": 0.6, "ytick.major.width": 0.6,
        "axes.spines.top": False, "axes.spines.right": False,
        "grid.color": GRIDC, "grid.linewidth": 0.6,
        "legend.frameon": False, "savefig.dpi": 300, "hatch.linewidth": 0.7,
        "axes.titlesize": 7.6, "axes.titleweight": "bold", "axes.titlecolor": TXT,
        "axes.titlelocation": "left",
    })


def rd(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return list(csv.DictReader(f))


def fnum(v):
    return float(v) if v not in (None, "") else None


def lighten(hexcol, f):
    h = hexcol.lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    r, g, b = (int(c + (255 - c) * f) for c in (r, g, b))
    return "#%02x%02x%02x" % (r, g, b)


def fig_mm(w, h):
    return plt.subplots(figsize=(w / 25.4, h / 25.4))


def ref_line(ax, y, label, horizontal=True):
    if horizontal:
        ax.axhline(y, color=MUTED, lw=0.8, ls=(0, (4, 3)), zorder=1)
    else:
        ax.axvline(y, color=MUTED, lw=0.8, ls=(0, (4, 3)), zorder=1)


def save(fig, path):
    fig.savefig(path, bbox_inches="tight", pad_inches=0.04)
    plt.close(fig)


def chart_headline(path, w):
    rows = rd("dense_plateaus.csv")
    fig, (a1, a2) = plt.subplots(1, 2, figsize=(w / 25.4, 54 / 25.4),
                                 gridspec_kw={"width_ratios": [4, 4.6], "wspace": 0.12}, sharey=True)
    b15 = {r["config"]: float(r["fps"]) for r in rows if r["run"] == "b15_best_plat" and r["plateau"] == "550"}
    keys = ["off", "prev", "new", "max"]
    for i, k in enumerate(keys):
        a1.bar(i, b15[k], width=0.62, color=COL[k], zorder=3)
        a1.text(i, b15[k] + 0.8, "%.1f" % b15[k], ha="center", va="bottom", fontsize=7.4,
                color=TXT, fontweight="bold" if k == "max" else "normal")
    a1.set_xticks(range(len(keys)))
    a1.set_xticklabels([SHORT[k] for k in keys])
    a1.set_title("Run b15_best_plat (build b15)")
    a1.set_ylabel("server fps (higher is better)")
    keys2 = ["off", "prev", "new", "all"]
    va = {r["config"]: float(r["fps"]) for r in rows if r["run"] == "b20_best" and r["plateau"] == "550"}
    vb = {r["config"]: float(r["fps"]) for r in rows if r["run"] == "b20_best2" and r["plateau"] == "550"}
    bw = 0.36
    for i, k in enumerate(keys2):
        a2.bar(i - bw / 2 - 0.01, va[k], width=bw, color=COL[k], zorder=3)
        a2.bar(i + bw / 2 + 0.01, vb[k], width=bw, color=lighten(COL[k], 0.45), edgecolor=COL[k],
               lw=0.8, hatch="////", zorder=3)
        a2.text(i - bw / 2 - 0.01, va[k] + 0.8, "%.1f" % va[k], ha="center", va="bottom", fontsize=5.9, color=TXT)
        a2.text(i + bw / 2 + 0.01, vb[k] + 0.8, "%.1f" % vb[k], ha="center", va="bottom", fontsize=5.9, color=TXT)
    a2.set_xticks(range(len(keys2)))
    a2.set_xticklabels([SHORT[k] for k in keys2])
    a2.set_title("Runs b20_best / b20_best2 (build b20)")
    a2.legend(handles=[Patch(facecolor="#9a9893", label="run b20_best"),
                       Patch(facecolor="#e2e1dd", edgecolor="#7d7b75", hatch="////", label="run b20_best2")],
              loc="upper left", fontsize=6.6, handlelength=1.6)
    for ax in (a1, a2):
        ax.set_ylim(0, 46)
        ax.grid(axis="y", zorder=0)
        ax.tick_params(axis="x", length=0)
        ref_line(ax, 30, "30 fps")
    a1.text(-0.45, 30.6, "30 fps", fontsize=6.4, color=MUTED, va="bottom")
    save(fig, path)


def chart_dense(path, w):
    rows = rd("dense_plateaus.csv")
    keys = ["off", "prev", "new", "new2", "all"]
    fig, axes = plt.subplots(1, 2, figsize=(w / 25.4, 64 / 25.4), gridspec_kw={"wspace": 0.16})
    for ax, pl, title, ymax in ((axes[0], "450", "450 plateau (~425 lab units)", 80),
                                (axes[1], "550", "550 plateau (~522 lab units)", 40)):
        va = {r["config"]: float(r["fps"]) for r in rows if r["run"] == "b20_best" and r["plateau"] == pl}
        vb = {r["config"]: float(r["fps"]) for r in rows if r["run"] == "b20_best2" and r["plateau"] == pl}
        bw = 0.38
        for i, k in enumerate(keys):
            ax.bar(i - bw / 2 - 0.01, va[k], width=bw, color=COL[k], zorder=3)
            ax.bar(i + bw / 2 + 0.01, vb[k], width=bw, color=lighten(COL[k], 0.45), edgecolor=COL[k],
                   lw=0.8, hatch="////", zorder=3)
            off = ymax * 0.012
            ax.text(i - bw / 2 - 0.01, va[k] + off, "%.1f" % va[k], ha="center", va="bottom", fontsize=5.9,
                    color=TXT, rotation=90)
            ax.text(i + bw / 2 + 0.01, vb[k] + off, "%.1f" % vb[k], ha="center", va="bottom", fontsize=5.9,
                    color=TXT, rotation=90)
        ax.set_xticks(range(len(keys)))
        ax.set_xticklabels([SHORT[k] for k in keys], fontsize=6.6)
        ax.tick_params(axis="x", length=0)
        ax.set_ylim(0, ymax)
        ax.grid(axis="y", zorder=0)
        ref_line(ax, 30, "30 fps")
        ax.set_title(title)
    axes[0].set_ylabel("server fps")
    axes[1].legend(handles=[Patch(facecolor="#9a9893", label="run b20_best"),
                            Patch(facecolor="#e2e1dd", edgecolor="#7d7b75", hatch="////", label="run b20_best2")],
                   loc="upper left", fontsize=6.6, handlelength=1.6)
    save(fig, path)


def chart_steps(path, w):
    rows = rd("dense_plateaus.csv")
    runs = [("b15_best_plat", ["off", "prev", "new", "max"], "b15_best_plat (build b15)"),
            ("b20_best", ["off", "prev", "new", "new2", "all"], "b20_best (build b20)"),
            ("b20_best2", ["off", "prev", "new", "new2", "all"], "b20_best2 (build b20)")]
    names = {"off": "Mod off", "prev": "Previous defaults", "new": "+ new invisible items",
             "max": "+ 5 s wreck removal", "new2": "+ prefiltered replay", "all": "+ worker-thread scans"}
    fig, axes = plt.subplots(3, 1, figsize=((w - 28) / 25.4, 88 / 25.4), sharex=True,
                             gridspec_kw={"height_ratios": [4, 5, 5], "hspace": 0.38})
    for ax, (run, keys, title) in zip(axes, runs):
        d = {r["config"]: r for r in rows if r["run"] == run and r["plateau"] == "550"}
        ys = list(range(len(keys)))[::-1]
        for y, k in zip(ys, keys):
            wv = float(d[k]["work_ms"])
            ax.barh(y, wv, height=0.66, color=COL[k], zorder=3)
            pct = d[k]["vs_off_pct"]
            lab = "%.1f ms" % wv + ("   %s %% vs mod off" % pct.replace("-", "\u2212") if pct else "")
            ax.text(wv + 1.2, y, lab, va="center", ha="left", fontsize=6.4, color=TXT)
        ax.set_yticks(ys)
        ax.set_yticklabels([names[k] for k in keys], fontsize=6.7)
        ax.tick_params(axis="y", length=0)
        ax.set_xlim(0, 125)
        ax.set_xticks([0, 20, 40, 60, 80, 100])
        ax.grid(axis="x", zorder=0)
        ax.spines["left"].set_visible(False)
        ax.set_title(title, fontsize=7.2)
    axes[-1].set_xlabel("frame work at the 550 plateau, ms (lower is better)")
    save(fig, path)


def chart_spread(path, w):
    rows = rd("spread.csv")
    fig, ax = fig_mm(w, 62)
    for k in ["off", "prev", "new", "all"]:
        pts = sorted((int(r["lab_units"]), float(r["fps"])) for r in rows if r["config"] == k)
        ax.plot([p[0] for p in pts], [p[1] for p in pts], color=COL[k], ls=LS[k], lw=1.6,
                marker=MARK[k], ms=5, mec="white", mew=0.7, label=LABEL[k], zorder=3)
    ref_line(ax, 30, "30 fps")
    ax.text(616, 31, "30 fps", fontsize=6.4, color=MUTED, va="bottom", ha="right")
    ax.set_xticks([422, 518, 614])
    ax.set_xlim(405, 630)
    ax.set_ylim(0, 85)
    ax.set_xlabel("living lab units (plateau mean)")
    ax.set_ylabel("server fps")
    ax.grid(axis="y")
    ax.legend(loc="upper right", fontsize=6.6, handlelength=3.0)
    save(fig, path)


def chart_crossings(path, w):
    rows = rd("crossings.csv")
    keys = ["off", "prev", "new"]
    fig, axes = plt.subplots(1, 3, figsize=((w - 30) / 25.4, 46 / 25.4), sharey=True,
                             gridspec_kw={"wspace": 0.08})
    for ax, col, title in zip(axes, ["below30", "below20", "below10"],
                              ["first below 30 fps", "first below 20 fps", "first below 10 fps"]):
        for y, k in enumerate(keys[::-1]):
            main = [r for r in rows if r["run"] == "b15_best_qc" and r["config"] == k][0]
            v = fnum(main[col])
            if v is None:
                ax.plot([690, 712], [y, y], color=COL[k], lw=1.2, zorder=3)
                ax.annotate("", xy=(718, y), xytext=(700, y),
                            arrowprops=dict(arrowstyle="-|>", color=COL[k], lw=1.2), zorder=3)
                ax.text(688, y, "not reached ", va="center", ha="right", fontsize=6.2, color=TXT)
            else:
                ax.plot([v], [y], marker=MARK[k], ms=7, color=COL[k], mec="white", mew=0.7, zorder=4,
                        ls="none")
                ax.text(v, y + 0.27, "%d" % v, ha="center", va="bottom", fontsize=6.3, color=TXT)
            for run in ("qc1", "qc2"):
                o = [r for r in rows if r["run"] == run and r["config"] == k]
                if o and fnum(o[0][col]) is not None:
                    ax.plot([fnum(o[0][col])], [y], marker="d", ms=4.2, color="white", mec=TXT2,
                            mew=0.8, ls="none", zorder=5)
        ax.set_xlim(415, 725)
        ax.set_ylim(-0.6, 2.75)
        ax.set_xticks([450, 500, 550, 600, 650, 700])
        ax.grid(axis="x", zorder=0)
        ax.set_title(title)
        ax.set_xlabel("living lab units")
        ax.tick_params(axis="y", length=0)
    axes[0].set_yticks(range(3))
    axes[0].set_yticklabels([LABEL[k] for k in keys[::-1]], fontsize=6.6)
    fig.legend(handles=[Line2D([], [], marker="o", ls="none", color="#7d7b75", ms=6,
                               label="run b15_best_qc (build b15)"),
                        Line2D([], [], marker="d", ls="none", color="white", mec=TXT2, ms=4.5,
                               label="runs qc1 and qc2 (build b10; mod off and previous defaults)")],
               loc="upper center", bbox_to_anchor=(0.55, -0.08), ncol=2, fontsize=6.4, handletextpad=0.3)
    save(fig, path)


def chart_trickle(path, w):
    fig, axes = plt.subplots(1, 2, figsize=((w - 48) / 25.4, 62 / 25.4), sharey=True,
                             gridspec_kw={"wspace": 0.08})

    def series(fname, arm):
        pts = sorted((int(r["u_bin_start"]) + 25, float(r["work_ms"])) for r in rd(fname) if r["arm"] == arm)
        return [p[0] for p in pts], [p[1] for p in pts]
    ax = axes[0]
    for k, arm in (("off", "off"), ("prev", "def"), ("new", "new")):
        x, y = series("trickle_bins_b15_best_qc.csv", arm)
        ax.plot(x, y, color=COL[k], ls=LS[k], lw=1.5, marker=MARK[k], ms=3.8, mec="white", mew=0.5,
                label=LABEL[k])
    ax.set_title("Run b15_best_qc (build b15)")
    ax.legend(loc="lower right", fontsize=6.4, handlelength=3.0)
    ax = axes[1]
    for k, arm in (("off", "off"), ("prev", "def"), ("agg", "agg")):
        for fname, ls, run in (("trickle_bins_qc1.csv", "-", "qc1"), ("trickle_bins_qc2.csv", (0, (3, 2)), "qc2")):
            x, y = series(fname, arm)
            ax.plot(x, y, color=COL[k], ls=ls, lw=1.3, marker=MARK[k], ms=3.4, mec="white", mew=0.5,
                    label="%s, %s" % (LABEL[k], run))
    ax.set_title("Runs qc1 (solid) and qc2 (dashed), build b10")
    ax.legend(loc="upper left", bbox_to_anchor=(1.02, 1.0), fontsize=6.0, handlelength=3.0, ncol=1)
    for ax in axes:
        ax.set_yscale("log")
        ax.set_yticks([2, 5, 10, 20, 50, 100])
        ax.set_yticklabels(["2", "5", "10", "20", "50", "100"])
        ax.set_ylim(2.5, 200)
        ax.set_xlim(50, 720)
        ax.grid(axis="both", which="major")
        ax.set_xlabel("living lab units (50-unit bins)")
        for fps in (30, 10):
            ax.axhline(1000 / fps, color=MUTED, lw=0.7, ls=(0, (4, 3)))
        ax.text(58, 1000 / 30 * 1.04, "33 ms = 30 fps", fontsize=6, color=MUTED, ha="left", va="bottom")
        ax.text(58, 100 * 1.04, "100 ms = 10 fps", fontsize=6, color=MUTED, ha="left", va="bottom")
    axes[0].set_ylabel("frame work, ms (log scale)")
    save(fig, path)


def chart_god(path, w):
    rows = rd("god_battles_wave1.csv")
    fig, ax = fig_mm(w, 58)
    for k in ["off", "prev", "agg"]:
        pts = sorted((int(r["world_units"]), float(r["fps_median"])) for r in rows if r["config"] == k)
        ax.plot([p[0] for p in pts], [p[1] for p in pts], color=COL[k], ls=LS[k], lw=1.6, marker=MARK[k],
                ms=5, mec="white", mew=0.7, label=LABEL[k], zorder=3)
    for k, dx, dy in (("off", -6, -9), ("prev", 0, 4)):
        for r in rows:
            if r["config"] == k:
                ax.text(int(r["world_units"]) + dx, float(r["fps_median"]) + dy, r["fps_median"], fontsize=6.2,
                        color=TXT, ha="center", va="bottom" if dy > 0 else "top")
    ref_line(ax, 60, "60")
    ref_line(ax, 30, "30")
    ax.text(303, 61, "60 fps", fontsize=6.2, color=MUTED, ha="left", va="bottom")
    ax.text(303, 31, "30 fps", fontsize=6.2, color=MUTED, ha="left", va="bottom")
    ax.set_xticks([329, 477, 617, 800])
    ax.set_xlim(300, 830)
    ax.set_ylim(0, 128)
    ax.set_xlabel("world units (all living units, no dead in god mode)")
    ax.set_ylabel("server fps (median)")
    ax.grid(axis="y")
    ax.legend(loc="upper right", fontsize=6.6, handlelength=3.0)
    save(fig, path)


def chart_wreck(path, w):
    rows = [r for r in rd("wreck_removal.csv") if r["fps_without"]]
    fig, ax = fig_mm(w - 52, 60)   # the y labels add ~50 mm to the left
    labels = []
    for y, r in enumerate(rows[::-1]):
        a, b = float(r["fps_without"]), float(r["fps_with"])
        ax.plot([a, b], [y, y], color="#b9b8b2", lw=2.2, zorder=2, solid_capstyle="round")
        ax.plot([a], [y], marker="o", ms=6.5, color="white", mec=TXT2, mew=1.2, zorder=3, ls="none")
        ax.plot([b], [y], marker="D", ms=6.2, color=COL["max"], mec="white", mew=0.6, zorder=4, ls="none")
        ax.text(a - 2.0, y, "%.1f" % a, ha="right", va="center", fontsize=6.4, color=TXT)
        note = ""
        if r["paired_change_pct"]:
            note = "   frame work %s %% (s.e. %s)" % (r["paired_change_pct"].replace("-", "\u2212"), r["paired_se"])
        ax.text(b + 2.0, y, "%.1f%s" % (b, note), ha="left", va="center", fontsize=6.4, color=TXT)
        lu = r["lab_units"]
        sc = r["scenario"] + (" (~%s lab units)" % lu.lstrip("~") if lu else "")
        labels.append("%s\n%s: %s, added to %s" % (
            sc, r["run"], "fixed 5 s" if r["removal_form"].startswith("fixed") else "fps-following",
            "new items" if r["base_config"] == "new" else "worker set"))
    ax.set_yticks(range(len(rows)))
    ax.set_yticklabels(labels[::-1][::-1], fontsize=6.5, linespacing=1.25)
    ax.tick_params(axis="y", length=0)
    ax.set_xlim(10, 100)
    ax.set_ylim(-0.6, len(rows) - 0.4)
    ax.set_xlabel("server fps")
    ax.grid(axis="x")
    ax.spines["left"].set_visible(False)
    ax.legend(handles=[Line2D([], [], marker="o", ls="none", color="white", mec=TXT2, mew=1.2, ms=6,
                              label="without faster removal"),
                       Line2D([], [], marker="D", ls="none", color=COL["max"], ms=6, label="with faster removal")],
              loc="upper left", fontsize=6.4)
    save(fig, path)


def chart_parked(path, w):
    rows = rd("parked.csv")
    scen = []
    for r in rows:
        if r["scenario"] not in scen:
            scen.append(r["scenario"])
    names = {scen[0]: "tight grid, 300 idle\nLightStrikers (phys_b12)",
             scen[1]: "spaced lots, mean slope\n11-13° (b20_parked_spaced)",
             scen[2]: "spaced slope lots, mean\n10-11° (b20_parked_spaced)"}
    fig, axes = plt.subplots(1, 2, figsize=((w - 32) / 25.4, 50 / 25.4), sharey=True,
                             gridspec_kw={"wspace": 0.1})
    for ax, field, xl, xmax, fmt in ((axes[0], "wheeled_asleep_pct", "wheeled bodies asleep, %", 118, "%.0f %%"),
                                     (axes[1], "physx_ms_per_s", "physics (PhysX) time, ms per second", 150, "%.1f")):
        for i, s in enumerate(scen[::-1]):
            for j, k in enumerate(["prev", "parked"]):
                r = [x for x in rows if x["scenario"] == s and x["config"] == k][0]
                v = float(r[field])
                y = i + (0.19 if k == "prev" else -0.19)
                ax.barh(y, v, height=0.34, color=COL[k], zorder=3)
                t = (fmt % v).replace(".0 ", " ") if field.endswith("pct") else ("%g" % v)
                ax.text(v + xmax * 0.012, y, t, va="center", ha="left", fontsize=6.2, color=TXT)
        ax.set_xlim(0, xmax)
        ax.set_xlabel(xl)
        ax.grid(axis="x", zorder=0)
        ax.spines["left"].set_visible(False)
        ax.tick_params(axis="y", length=0)
    axes[0].set_xticks([0, 25, 50, 75, 100])
    axes[0].set_yticks(range(len(scen)))
    axes[0].set_yticklabels([names[s] for s in scen[::-1]], fontsize=6.4)
    fig.legend(handles=[Patch(color=COL["prev"], label="previous defaults"),
                        Patch(color=COL["parked"], label="previous defaults + parked-vehicle set")],
               loc="lower center", bbox_to_anchor=(0.55, -0.2), ncol=2, fontsize=6.5)
    save(fig, path)


def chart_knee(path, w):
    rows = rd("saturation_qc1_off.csv")
    fig, ax = fig_mm(w, 52)
    dark, light = "#1c5cab", "#cde2fb"
    for y, r in enumerate(rows[::-1]):
        t = float(r["time_driven_share_pct"])
        ax.barh(y, t, height=0.6, color=dark, zorder=3)
        ax.barh(y, 100 - t, left=t, height=0.6, color=light, zorder=3)
        ax.text(t - 1.5, y, "%d %%" % t, ha="right", va="center", fontsize=6.6, color="white", zorder=4)
        ax.text(101.5, y, "%s fps" % r["fps"], ha="left", va="center", fontsize=6.8, color=TXT,
                fontweight="bold")
    ax.set_yticks(range(len(rows)))
    ax.set_yticklabels(["%s lab units" % r["lab_units"] for r in rows[::-1]], fontsize=6.7)
    ax.tick_params(axis="y", length=0)
    ax.set_xlim(0, 112)
    ax.set_xticks([0, 20, 40, 60, 80, 100])
    ax.set_xlabel("share of each second of main-thread time, %")
    ax.spines["left"].set_visible(False)
    ax.legend(handles=[Patch(color=dark, label="time-driven work T' (physics steps, AI ticks, projectiles ...)"),
                       Patch(color=light, label="left for frames")],
              loc="upper center", bbox_to_anchor=(0.45, -0.3), ncol=2, fontsize=6.4)
    save(fig, path)


CHARTS_FN = {"headline": chart_headline, "dense": chart_dense, "steps": chart_steps,
             "spread": chart_spread, "crossings": chart_crossings, "trickle": chart_trickle,
             "god": chart_god, "wreck": chart_wreck, "parked": chart_parked, "knee": chart_knee}


# ----------------------------------------------------- flowable builders ---
def table_widths(header, rows):
    n = len(header)
    pad = 7.0
    nat, mn = [0.0] * n, [0.0] * n
    for ri, row in enumerate([header] + rows):
        bold = ri == 0
        group = is_group(row) and ri > 0
        for c in range(n):
            if group:
                continue
            nat[c] = max(nat[c], text_width(row[c], bold, TABLE_SIZE) + pad)
            mn[c] = max(mn[c], min_token_width(row[c], bold, TABLE_SIZE) + pad + 2)
    mn = [max(m, 16) for m in mn]
    nat = [max(a, b) for a, b in zip(nat, mn)]
    if sum(nat) <= AVAIL_W:
        target = AVAIL_W if sum(nat) > 0.62 * AVAIL_W else max(sum(nat) * 1.25, 0.62 * AVAIL_W)
        extra = target - sum(nat)
        return [w + extra * w / sum(nat) for w in nat]
    if sum(mn) <= AVAIL_W:
        widths = list(mn)
        room = AVAIL_W - sum(mn)
        short = [c for c in range(n) if nat[c] <= 0.22 * AVAIL_W and nat[c] > mn[c]]
        need = sum(nat[c] - mn[c] for c in short)
        if need and room > 0:
            f = min(1.0, (room * 0.75) / need)
            for c in short:
                widths[c] += (nat[c] - mn[c]) * f
            room = AVAIL_W - sum(widths)
        flex = [nat[c] - widths[c] for c in range(n)]
        if sum(flex) > 0:
            widths = [w + room * fl / sum(flex) for w, fl in zip(widths, flex)]
        return widths
    print("WARNING: table min widths %.0f pt > %.0f pt; scaling down" % (sum(mn), AVAIL_W))
    return [m * AVAIL_W / sum(mn) for m in mn]


def is_group(row):
    return row[0].startswith("**") and row[0].endswith("**") and all(not c for c in row[1:]) \
        and len(row) > 1


def build_table(header, aligns, rows, st):
    widths = table_widths(header, rows)
    data = [[Paragraph(inline(h, TABLE_SIZE), st["head" + aligns[c]]) for c, h in enumerate(header)]]
    style = [
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 2.4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 2.6),
        ("LEFTPADDING", (0, 0), (-1, -1), 3.5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 3.5),
        ("BACKGROUND", (0, 0), (-1, 0), HEAD_BG),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, ALT_BG]),
        ("INNERGRID", (0, 0), (-1, -1), 0.3, GRID),
        ("BOX", (0, 0), (-1, -1), 0.6, colors.HexColor("#a7afba")),
        ("LINEBELOW", (0, 0), (-1, 0), 0.9, colors.HexColor("#6a7a8f")),
    ]
    for ri, row in enumerate(rows, start=1):
        if is_group(row):
            data.append([Paragraph(inline(row[0], TABLE_SIZE), st["cellL"])] + [""] * (len(row) - 1))
            style += [("SPAN", (0, ri), (-1, ri)), ("BACKGROUND", (0, ri), (-1, ri), GROUP_BG)]
        else:
            data.append([Paragraph(inline(v, TABLE_SIZE), st["cell" + aligns[c]]) for c, v in enumerate(row)])
    t = Table(data, colWidths=widths, repeatRows=1, hAlign="LEFT")
    t.setStyle(TableStyle(style))
    t.spaceBefore = 1
    t.spaceAfter = 9
    return t


def build_list(items, st):
    out = []
    for lvl, text in items:
        out.append(Paragraph(inline(text, BODY_SIZE), st["list2" if lvl else "list"],
                             bulletText="\u2013" if lvl else "\u2022"))
    return out


class SectionRule(HRFlowable):
    pass


def figure_flowable(name, width_mm, cap, st):
    path = os.path.join(CHARTS, name + ".png")
    CHARTS_FN[name](path, width_mm)
    iw, ih = ImageReader(path).getSize()
    w = min(iw / 300.0 * 72.0, AVAIL_W)   # natural size (300 dpi), so chart fonts keep their point size
    img = Image(path, width=w, height=w * ih / iw)
    img.hAlign = "CENTER"
    return KeepTogether([Spacer(1, 2), img, Paragraph(inline(cap, 7.9), st["caption"])])


_MEASURE = None


def height_of(f, cap=None):
    global _MEASURE
    if _MEASURE is None:
        _MEASURE = rl_canvas.Canvas(io.BytesIO(), pagesize=A4)
    if isinstance(f, KeepTogether):
        return sum(height_of(c) for c in f._content)
    h = f.wrapOn(_MEASURE, AVAIL_W, PAGE_H)[1]
    if isinstance(f, Table):
        h = sum(f._rowHeights[:3])
    h += f.getSpaceBefore() + f.getSpaceAfter()
    return min(h, cap) if cap else h


class Keep:
    def __init__(self, f):
        self.f = f


def resolve_keeps(items):
    out, k = [], 0
    while k < len(items):
        if not isinstance(items[k], Keep):
            out.append(items[k])
            k += 1
            continue
        chain = []
        while k < len(items) and isinstance(items[k], Keep):
            chain.append(items[k].f)
            k += 1
        need = sum(height_of(f) for f in chain)
        if k < len(items):
            nf = items[k]
            need += height_of(nf) if isinstance(nf, KeepTogether) else height_of(nf, cap=30 * mm)
        out.append(CondPageBreak(min(need + 2, 0.85 * (PAGE_H - 2 * MARGIN))))
        out.extend(chain)
    return out


def assemble(blocks, st):
    out = []
    for j, b in enumerate(blocks):
        nxt = blocks[j + 1][0] if j + 1 < len(blocks) else None
        if b[0] == "h":
            style = st["h2"] if b[1] <= 2 else st["h3"]
            out.append(Keep(Paragraph(inline(b[2], style.fontSize), style)))
        elif b[0] == "pb":
            out.append(PageBreak())
        elif b[0] == "p":
            text = " ".join(b[1])
            p = Paragraph(inline(text, BODY_SIZE), st["body"])
            keep = plain(text).rstrip().endswith(":") or nxt in ("table", "ul", "cap")
            out.append(Keep(p) if keep else p)
        elif b[0] == "ul":
            paras = build_list(b[1], st)
            for p in paras[:-1]:
                out.append(p)
            out.append(paras[-1])
        elif b[0] == "cap":
            out.append(Keep(Paragraph(inline(b[1], 7.9), st["tcap"])))
        elif b[0] == "table":
            out.append(build_table(b[1], b[2], b[3], st))
        elif b[0] == "fig":
            out.append(figure_flowable(b[1], b[2], b[3], st))
    return resolve_keeps(out)


# --------------------------------------------------------- title page -----
def title_page(st):
    f = [Spacer(1, 38 * mm),
         Paragraph(TITLE, st["t_title"]),
         Paragraph(SUBTITLE, st["t_sub"]),
         HRFlowable(width="100%", thickness=1.4, color=ACCENT, spaceBefore=2, spaceAfter=10),
         Paragraph("Silica dedicated server 1.0.17 (headless) &middot; mod version 0.2.0, build b21", st["t_meta"]),
         Paragraph("Lab measurements of 16 and 17 November 2026 (wave 1 and night 1)", st["t_meta"]),
         Paragraph("Author: <b>%s</b> &middot; %s" % (AUTHOR, DATE), st["t_meta"]),
         Spacer(1, 16 * mm)]
    box = [
        Paragraph("<b>In brief</b>", st["t_box"]),
        Paragraph(inline("Si_ServerPerf is a server-only mod that removes main-thread work a headless Silica server "
                         "does not need and does some necessary work more cheaply, mostly with exactly the same "
                         "result as the original game. In the hardest lab test, a dense three-team fight with "
                         "about 522 living units, the configuration closest to the 0.2.0 defaults ran at "
                         "**40.1 fps** where the same server with the mod switched off ran at **15.6 fps**. "
                         "The one change players can see is that corpses and wrecks disappear after 5 s instead "
                         "of 15 s. Everything was measured on a lab server without players; a test with players "
                         "is still open.", 9.6), st["t_box"]),
        Paragraph(inline("Readers: server hosts and developers who know Silica. Section 1 is a one-page summary; "
                         "section 3 defines the lab's terms.", 9.6), st["t_box"]),
    ]
    t = Table([[box]], colWidths=[AVAIL_W])
    t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#f2f5f9")),
                           ("BOX", (0, 0), (-1, -1), 0.6, colors.HexColor("#a7afba")),
                           ("LEFTPADDING", (0, 0), (-1, -1), 10), ("RIGHTPADDING", (0, 0), (-1, -1), 10),
                           ("TOPPADDING", (0, 0), (-1, -1), 8), ("BOTTOMPADDING", (0, 0), (-1, -1), 4)]))
    f.append(t)
    f.append(PageBreak())
    return f


# --------------------------------------------------------- page template ---
class NumberedCanvas(rl_canvas.Canvas):
    def __init__(self, *a, **kw):
        super().__init__(*a, **kw)
        self._saved = []

    def showPage(self):
        self._saved.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        total = len(self._saved)
        for state in self._saved:
            self.__dict__.update(state)
            if self._pageNumber > 1:
                self.draw_footer(total)
            super().showPage()
        super().save()

    def draw_footer(self, total):
        y = 10 * mm
        self.saveState()
        self.setStrokeColor(GRID)
        self.setLineWidth(0.5)
        self.line(MARGIN, y + 3.6 * mm, PAGE_W - MARGIN, y + 3.6 * mm)
        self.setFont("Body", 7.4)
        self.setFillColor(INK2)
        self.drawString(MARGIN, y, FOOTER_LEFT)
        self.drawRightString(PAGE_W - MARGIN, y, "page %d of %d" % (self._pageNumber, total))
        self.restoreState()


def glyph_check(md):
    missing = {}
    text = protect_numbers(md)
    code = "".join(re.findall(r"`([^`]+)`", text)) + ".\u200b_"
    rest = re.sub(r"`[^`]+`", "", text) + FOOTER_LEFT + "page of 0123456789\u2022\u2013\u00b7"
    for fam, chars in (("Body", rest), ("Cond", rest), ("Mono", code)):
        for v in ("", "B", "I"):
            cmap = pdfmetrics.getFont(fam + v).face.charToGlyph
            for ch in set(chars):
                if ch not in "\r\n\t\u200b" and ord(ch) not in cmap:
                    missing.setdefault(fam + v, set()).add(ch)
    return missing


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=DEFAULT_OUT)
    a = ap.parse_args()
    os.makedirs(CHARTS, exist_ok=True)
    register_fonts()
    chart_style()
    md = open(SOURCE, encoding="utf-8").read()
    missing = glyph_check(md)
    if missing:
        for f, chs in missing.items():
            print("MISSING GLYPHS in %s: %s" % (f, " ".join("U+%04X" % ord(c) for c in sorted(chs))))
        sys.exit(1)
    st = make_styles()
    story = title_page(st) + assemble(parse_blocks(md), st)
    doc = SimpleDocTemplate(a.out, pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN,
                            topMargin=MARGIN, bottomMargin=MARGIN + 2 * mm,
                            title="Si_ServerPerf 0.2.0: server performance report", author=AUTHOR,
                            subject="Silica dedicated server performance mod", creator="build_report.py")
    doc.build(story, canvasmaker=NumberedCanvas)
    print("wrote", a.out)


if __name__ == "__main__":
    main()
