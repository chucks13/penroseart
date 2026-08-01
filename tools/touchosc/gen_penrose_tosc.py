#!/usr/bin/env python3
"""Generate the Penrose TouchOSC control surface as raw LexML XML.

Every action on this surface is a real ``BUTTON``. TouchOSC's BUTTON control has
no caption of its own, so each action is two overlaid nodes: the BUTTON that owns
the touch and the pressed-fill feedback, and a non-interactive ``TEXT`` node that
carries the readable caption. ``TEXT`` rather than ``LABEL`` because effect names
such as "Random Effects Mixer" must wrap inside an 88px cell, and LABEL is
single-line only.

Schema facts verified against the installed binary (/Applications/TouchOSC.app,
1.5.2 build 262) plus Hexler's official manual:

  * root is <lexml version='6'>, single-quoted attributes
  * string payloads are CDATA-wrapped
  * property type codes: b i f s c r
  * node ``type`` is the control-type name: BOX BUTTON LABEL TEXT FADER GROUP ...
  * <properties>/<values>/<messages>/<children> are each optional; omitted
    blocks fall back to constructor defaults, so only verified property keys
    are emitted here
  * <connections> is exactly ten chars, rightmost = Connection 1
  * feedback=0 suppresses an outgoing send whose triggering value change
    arrived through the same descriptor
  * Orientation enum: NORTH=0 EAST=1 SOUTH=2 WEST=3
  * buttonType enum: MOMENTARY=0 TOGGLE_RELEASE=1 TOGGLE_PRESS=2

Button behaviour (Hexler manual, controls + editor-messages-osc):

  * BUTTON's ``x`` value is a float 0..1 and the button fill is rendered with
    ``x`` as its alpha, so a Momentary BUTTON shows a pressed state locally with
    no OSC round trip and no script.
  * The press-only send is trigger ``x RISE`` with a CONSTANT FLOAT 1 argument.
  * A control is a pointer target only when it is both visible and interactive,
    so the caption overlay sets interactive=0 to let the BUTTON beneath own the
    touch. grabFocus=0 alone does not make an overlay touch-through.

Run:  python3 tools/touchosc/gen_penrose_tosc.py
"""

from __future__ import annotations

import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

NS = uuid.UUID("6f1d1f2a-0b3c-4d5e-8a9b-penrose000000".replace("penrose", "9c7e5d"))

OUT_PATH = Path(__file__).resolve().parent / "penrose.tosc"

CANVAS_W, CANVAS_H = 1024, 640
CONNECTION_1 = "0000000001"

# --- Palette -----------------------------------------------------------------
# One cool primary for content, one utility blue for continuous controls, and two
# warm accents reserved for the actions that change what the deck is doing.
CHASSIS = (0.055, 0.063, 0.075, 1.0)  # #0E1013
EFFECT = (0.231, 0.820, 0.690)  # #3BD1B0 teal
NYE = (0.949, 0.627, 0.239)  # #F2A03D amber
RESUME = (0.910, 0.333, 0.427)  # #E8556D rose
FADER_BLUE = (0.357, 0.553, 0.937)  # #5B8DEF
CAPTION_INK = (0.541, 0.576, 0.627, 1.0)  # #8A93A0 section captions
BUTTON_INK = (0.949, 0.961, 0.973, 1.0)  # #F2F5F8 button legends
# The live ring is deliberately not one of the accent hues: it has to mean the same
# thing on a teal effect cell and on the rose Resume button.
LIVE_RING = (0.949, 0.961, 0.973, 1.0)  # #F2F5F8
RING_WIDTH = 3

# --- Geometry ----------------------------------------------------------------
MARGIN = 32
# Each fader's header is a stacked pair: a muted caption naming the control, and
# the live value under it in the largest type on the surface, because the number
# is what you scan for across a dark room.
STRIP_CAP_Y, STRIP_CAP_H, STRIP_CAP_SIZE = 18, 14, 11
STRIP_VAL_Y, STRIP_VAL_H, STRIP_VAL_SIZE = 33, 24, 19
STRIP_Y, STRIP_H = 62, 66

BRIGHT_X, BRIGHT_W = 32, 88
GRID_X = 136
COLS, ROWS = 9, 3
CELL_W, CELL_H = 88, 148
GAP_X, GAP_Y = 8, 12
GRID_Y = 140
GRID_RIGHT = GRID_X + COLS * CELL_W + (COLS - 1) * GAP_X  # 992

PERIOD_X, PERIOD_W = GRID_X, 420
NYE_X, NYE_W = 572, 196
RESUME_X, RESUME_W = 784, 208

CORNER_RADIUS = 3.0

EFFECT_LABELS = (
    "Angles",
    "Animate Loops",
    "Color Sparkle",
    "Crystal Growth",
    "Flock",
    "Fluid",
    "Julia",
    "Kscope",
    "Lightning",
    "Maze Flyer",
    "Meta Balls",
    "Mirror",
    "Nibbler",
    "Noise",
    "Noise Mixer",
    "Noise Tunnel",
    "Petals",
    "Pulse",
    "Rainbow Bars",
    "Random Effects Mixer",
    "Ripple",
    "Shape Glitch",
    "Tile Shapes",
    "Tunnel",
    "Vortex",
    "Waterfall",
    "Yin Yang Mixer",
)

assert len(EFFECT_LABELS) == COLS * ROWS
assert GRID_RIGHT == CANVAS_W - MARGIN, GRID_RIGHT
assert RESUME_X + RESUME_W == GRID_RIGHT
assert GRID_Y + ROWS * CELL_H + (ROWS - 1) * GAP_Y == CANVAS_H - MARGIN


def nid(name: str) -> str:
    """Derive a stable node ID so regenerating the layout does not churn IDs."""
    return str(uuid.uuid5(NS, name))


def cdata(text: str) -> str:
    return f"<![CDATA[{text}]]>"


def prop(type_code: str, key: str, value: str, indent: str) -> str:
    return (
        f"{indent}<property type='{type_code}'>\n"
        f"{indent}  <key>{cdata(key)}</key>\n"
        f"{indent}  <value>{value}</value>\n"
        f"{indent}</property>\n"
    )


def prop_s(key: str, value: str, indent: str) -> str:
    return prop("s", key, cdata(value), indent)


def prop_b(key: str, value: bool, indent: str) -> str:
    return prop("b", key, "1" if value else "0", indent)


def prop_i(key: str, value: int, indent: str) -> str:
    return prop("i", key, str(value), indent)


def prop_f(key: str, value: float, indent: str) -> str:
    return prop("f", key, str(value), indent)


def prop_frame(x: int, y: int, w: int, h: int, indent: str) -> str:
    return prop("r", "frame", f"<x>{x}</x><y>{y}</y><w>{w}</w><h>{h}</h>", indent)


def prop_color(rgba: tuple[float, ...], indent: str, key: str = "color") -> str:
    r, g, b = rgba[0], rgba[1], rgba[2]
    a = rgba[3] if len(rgba) > 3 else 1.0
    return prop("c", key, f"<r>{r}</r><g>{g}</g><b>{b}</b><a>{a}</a>", indent)


def partial(ptype: str, conversion: str, value: str, indent: str) -> str:
    return (
        f"{indent}<partial>\n"
        f"{indent}  <type>{ptype}</type>\n"
        f"{indent}  <conversion>{conversion}</conversion>\n"
        f"{indent}  <value>{cdata(value)}</value>\n"
        f"{indent}  <scaleMin>0</scaleMin>\n"
        f"{indent}  <scaleMax>1</scaleMax>\n"
        f"{indent}</partial>\n"
    )


def control_value(key: str, default: str, indent: str, *, lock_current: bool = False) -> str:
    """Serialize one control value and the current value it starts at."""
    return (
        f"{indent}<value>\n"
        f"{indent}  <key>{cdata(key)}</key>\n"
        f"{indent}  <locked>0</locked>\n"
        f"{indent}  <lockedDefaultCurrent>{1 if lock_current else 0}</lockedDefaultCurrent>\n"
        f"{indent}  <default>{cdata(default)}</default>\n"
        f"{indent}  <defaultPull>0</defaultPull>\n"
        f"{indent}</value>\n"
    )


def osc_message(
    address: str,
    *,
    send: int,
    receive: int,
    trigger_var: str | None,
    condition: str | None,
    argument_type: str,
    argument_conversion: str,
    argument_value: str,
    indent: str,
) -> str:
    """Build one OSC descriptor with an optional local-value trigger."""
    triggers = ""
    if trigger_var is not None:
        assert condition is not None
        triggers = (
            f"{indent}  <triggers>\n"
            f"{indent}    <trigger>\n"
            f"{indent}      <var>{cdata(trigger_var)}</var>\n"
            f"{indent}      <condition>{condition}</condition>\n"
            f"{indent}    </trigger>\n"
            f"{indent}  </triggers>\n"
        )

    return (
        f"{indent}<osc>\n"
        f"{indent}  <enabled>1</enabled>\n"
        f"{indent}  <send>{send}</send>\n"
        f"{indent}  <receive>{receive}</receive>\n"
        f"{indent}  <feedback>0</feedback>\n"
        f"{indent}  <noDuplicates>0</noDuplicates>\n"
        f"{indent}  <connections>{CONNECTION_1}</connections>\n"
        + triggers
        + f"{indent}  <path>\n"
        + partial("CONSTANT", "STRING", address, indent + "    ")
        + f"{indent}  </path>\n"
        f"{indent}  <arguments>\n"
        + partial(argument_type, argument_conversion, argument_value, indent + "    ")
        + f"{indent}  </arguments>\n"
        f"{indent}</osc>\n"
    )


def node(
    name: str,
    ntype: str,
    body_props: str,
    messages: str,
    indent: str,
    children: str = "",
    values: str = "",
) -> str:
    out = f"{indent}<node ID='{nid(name)}' type='{ntype}'>\n"
    out += f"{indent}  <properties>\n{body_props}{indent}  </properties>\n"
    if values:
        out += f"{indent}  <values>\n{values}{indent}  </values>\n"
    out += f"{indent}  <messages>\n{messages}{indent}  </messages>\n"
    if children:
        out += f"{indent}  <children>\n{children}{indent}  </children>\n"
    out += f"{indent}</node>\n"
    return out


# A fader's readout must state what Penrose does with the position, not the raw
# 0..1 the wire carries. Each script below mirrors one Controller method, so a
# change there is a change here. The readout is a sibling LABEL, found by name.

# Controller.ApplyTouchOscCommand: brightness = Value.Lerp(0f, 255f), so the fader
# reads the normal way round and x is the level directly.
BRIGHTNESS_SCRIPT = """\
function refresh()
  local out = self.parent.children.val_brightness
  if out == nil then return end
  out.values.text = string.format('%d%%', math.floor(self.values.x * 100 + 0.5))
end

function init() refresh() end

function onValueChanged(key)
  if key == 'x' then refresh() end
end

function onReceiveOSC(message, connections)
  -- The wall heartbeats its state to every surface at once, which is what keeps two
  -- operators agreeing. A hand on this fader outranks that: dropping the echo mid-drag
  -- stops the wall from fighting the finger, and the next heartbeat after release
  -- resettles the fader anyway.
  return self.values.touch
end
"""

# Controller.EffectPeriodSteps: five detents, not a continuous value. The bands
# below are the midpoints between them and must stay in step with that table --
# Controller decides the period, this only names the one it will pick.
PERIOD_SCRIPT = """\
function periodText(p)
  if p < 0.125 then return '1 s' end
  if p < 0.375 then return '5 s' end
  if p < 0.625 then return '10 s' end
  if p < 0.875 then return '2 min' end
  return '60 min'
end

function refresh()
  local out = self.parent.children.val_period
  if out == nil then return end
  out.values.text = periodText(self.values.x)
end

function init() refresh() end

function onValueChanged(key)
  if key == 'x' then refresh() end
end

function onReceiveOSC(message, connections)
  -- The wall heartbeats its state to every surface at once, which is what keeps two
  -- operators agreeing. A hand on this fader outranks that: dropping the echo mid-drag
  -- stops the wall from fighting the finger, and the next heartbeat after release
  -- resettles the fader anyway.
  return self.values.touch
end
"""


def fader(
    name: str,
    address: str,
    x: int,
    y: int,
    w: int,
    h: int,
    orientation: int,
    receive: int,
    script: str,
    indent: str,
) -> str:
    """A continuous control that sends its live position and may be echoed back.

    ``script`` drives the fader's readout label; a script fault leaves the label
    showing its default text rather than breaking the control.
    """
    pi = indent + "    "
    props = (
        prop_s("name", name, pi)
        + prop_frame(x, y, w, h, pi)
        + prop_color(FADER_BLUE, pi)
        + prop_i("orientation", orientation, pi)
        + prop_b("background", True, pi)
        + prop_b("outline", True, pi)
        + prop_i("outlineStyle", 0, pi)
        + prop_f("cornerRadius", CORNER_RADIUS, pi)
        + prop_b("grid", False, pi)
        + prop_b("bar", True, pi)
        + prop_b("cursor", True, pi)
        + prop_s("script", script, pi)
    )
    messages = osc_message(
        address,
        send=1,
        receive=receive,
        trigger_var="x",
        condition="ANY",
        argument_type="VALUE",
        argument_conversion="FLOAT",
        argument_value="x",
        indent=pi,
    )
    return node(name, "FADER", props, messages, indent)


def text_row(
    name: str,
    text: str,
    x: int,
    y: int,
    w: int,
    h: int,
    size: int,
    ink: tuple[float, ...],
    indent: str,
    *,
    lock_current: bool,
) -> str:
    """A left-aligned, never-interactive line of text in a fader's header stack.

    ``lock_current`` is off for a readout, whose ``text`` a script overwrites at
    runtime, and on for a fixed caption.
    """
    pi = indent + "    "
    props = (
        prop_s("name", name, pi)
        + prop_frame(x, y, w, h, pi)
        + prop_b("visible", True, pi)
        + prop_b("interactive", False, pi)
        + prop_b("background", False, pi)
        + prop_b("outline", False, pi)
        + prop_b("grabFocus", False, pi)
        + prop_i("textSize", size, pi)
        + prop_i("textAlignH", 0, pi)
        + prop_i("textAlignV", 1, pi)
        + prop_color(ink, pi, key="textColor")
        + prop_b("textClip", True, pi)
    )
    values = control_value("text", text, pi, lock_current=lock_current)
    return node(name, "LABEL", props, "", indent, values=values)


def section_caption(name: str, text: str, x: int, w: int, indent: str) -> str:
    """The muted caption naming the fader below it."""
    return text_row(
        name, text, x, STRIP_CAP_Y, w, STRIP_CAP_H, STRIP_CAP_SIZE, CAPTION_INK, indent,
        lock_current=True,
    )


def readout(name: str, initial: str, x: int, w: int, indent: str) -> str:
    """The live value of the fader below it, rewritten by that fader's script."""
    return text_row(
        name, initial, x, STRIP_VAL_Y, w, STRIP_VAL_H, STRIP_VAL_SIZE, BUTTON_INK, indent,
        lock_current=False,
    )


def live_ring(name: str, address: str, x: int, y: int, w: int, h: int, indent: str) -> str:
    """The border that marks an action as live, lit by the wall and nothing else.

    A plate inflated by :data:`RING_WIDTH` on every side and drawn before the
    BUTTON, so the button covers its middle and only the border shows. This is a
    separate node because the built-in pressed fill already owns the button's ``x``:
    driving ``x`` from the wall as well would make a lit cell unable to show a press,
    and the momentary release would blank the lamp on an effect that is still live.

    The wall's 1/0 drives ``background`` rather than ``visible``. An invisible
    control may not process incoming messages, which would leave a dark ring no
    message could ever light again; an unpainted background cannot trap itself.
    """
    pi = indent + "    "
    props = (
        prop_s("name", name, pi)
        + prop_frame(x - RING_WIDTH, y - RING_WIDTH, w + 2 * RING_WIDTH, h + 2 * RING_WIDTH, pi)
        + prop_color(LIVE_RING, pi)
        + prop_b("visible", True, pi)
        + prop_b("interactive", False, pi)
        + prop_b("background", False, pi)  # dark until the wall says this one is live
        + prop_b("outline", False, pi)
        + prop_b("grabFocus", False, pi)
        + prop_f("cornerRadius", CORNER_RADIUS + 1, pi)
    )
    messages = osc_message(
        address,
        send=0,
        receive=1,
        trigger_var=None,
        condition=None,
        argument_type="PROPERTY",
        argument_conversion="BOOLEAN",
        argument_value="background",
        indent=pi,
    )
    return node(name, "BOX", props, messages, indent)


def action(
    name: str,
    caption: str,
    address: str,
    x: int,
    y: int,
    w: int,
    h: int,
    color: tuple[float, float, float],
    text_size: int,
    indent: str,
    *,
    ring: bool = False,
) -> str:
    """One pressable action: a Momentary BUTTON plus its non-interactive caption.

    The BUTTON owns the touch, the pressed fill, and the outgoing message. The
    caption is a separate TEXT node on the identical frame with interactive=0, so
    it renders over the button without ever becoming the pointer target.

    ``ring`` adds the live-state border underneath, for the actions whose state the
    wall actually reports. Emission order is ring, button, caption: later siblings
    paint over earlier ones.
    """
    pi = indent + "    "
    out = live_ring(f"{name}_ring", address, x, y, w, h, indent) if ring else ""

    button_props = (
        prop_s("name", name, pi)
        + prop_frame(x, y, w, h, pi)
        + prop_color(color, pi)
        + prop_b("visible", True, pi)
        + prop_b("interactive", True, pi)
        + prop_b("background", True, pi)
        + prop_b("outline", True, pi)
        + prop_i("outlineStyle", 0, pi)
        + prop_b("grabFocus", True, pi)
        + prop_f("cornerRadius", CORNER_RADIUS, pi)
        + prop_i("buttonType", 0, pi)  # MOMENTARY
        + prop_b("press", True, pi)
        + prop_b("release", True, pi)
        + prop_b("valuePosition", False, pi)
    )
    # x RISE with a constant payload: one message on press, none on release, and a
    # payload that does not depend on the button's live pressure value.
    button_messages = osc_message(
        address,
        send=1,
        receive=0,
        trigger_var="x",
        condition="RISE",
        argument_type="CONSTANT",
        argument_conversion="FLOAT",
        argument_value="1",
        indent=pi,
    )

    caption_props = (
        prop_s("name", f"{name}_cap", pi)
        + prop_frame(x, y, w, h, pi)
        + prop_b("visible", True, pi)
        + prop_b("interactive", False, pi)
        + prop_b("background", False, pi)
        + prop_b("outline", False, pi)
        + prop_b("grabFocus", False, pi)
        + prop_i("textSize", text_size, pi)
        + prop_i("textAlignH", 1, pi)
        + prop_i("textAlignV", 1, pi)
        + prop_color(BUTTON_INK, pi, key="textColor")
        + prop_b("textClip", True, pi)
        + prop_b("textWrap", True, pi)
    )
    caption_values = control_value("text", caption, pi, lock_current=True)

    return (
        out
        + node(name, "BUTTON", button_props, button_messages, indent)
        + node(f"{name}_cap", "TEXT", caption_props, "", indent, values=caption_values)
    )


def build() -> str:
    ci = "    "
    children = ""

    # Brightness. Bidirectional: Penrose echoes the current level back, so the
    # readout follows the wall even when something else changed the level.
    children += section_caption("cap_brightness", "BRIGHTNESS", BRIGHT_X, BRIGHT_W, ci)
    children += readout("val_brightness", "0%", BRIGHT_X, BRIGHT_W, ci)
    children += fader(
        "vscroll1",
        "/1/vscroll1",
        BRIGHT_X,
        STRIP_Y,
        BRIGHT_W,
        CANVAS_H - MARGIN - STRIP_Y,
        orientation=0,
        receive=1,
        script=BRIGHTNESS_SCRIPT,
        indent=ci,
    )

    # Effect period. Bidirectional like brightness: the period can change from the
    # Inspector or from another surface, and this fader has to follow it.
    children += section_caption("cap_period", "EFFECT PERIOD", PERIOD_X, PERIOD_W, ci)
    children += readout("val_period", "1 s", PERIOD_X, PERIOD_W, ci)
    children += fader(
        "hscroll1", "/1/hscroll1", PERIOD_X, STRIP_Y, PERIOD_W, STRIP_H,
        orientation=1, receive=1, script=PERIOD_SCRIPT, indent=ci,
    )

    # No ring on NYE: the wall never reports the NYE flag, so there is no state to show.
    children += action(
        "nav1", "TOGGLE NYE", "/1/nav1",
        NYE_X, STRIP_Y, NYE_W, STRIP_H, NYE, 17, ci,
    )
    # Resume rings while a hold is in force -- the button that releases the freeze is
    # the one that shows it, and every surface sees that someone has pinned an effect.
    children += action(
        "reset", "RESUME ROTATION", "/1/reset",
        RESUME_X, STRIP_Y, RESUME_W, STRIP_H, RESUME, 17, ci, ring=True,
    )

    # The 27 cell addresses match the runtime catalog order one-for-one, so cell N
    # sends /1/pushN and Controller reads it as zero-based effect index N-1.
    for index, effect_label in enumerate(EFFECT_LABELS):
        row, col = divmod(index, COLS)
        cell = index + 1
        children += action(
            f"push{cell}",
            effect_label,
            f"/1/push{cell}",
            GRID_X + col * (CELL_W + GAP_X),
            GRID_Y + row * (CELL_H + GAP_Y),
            CELL_W,
            CELL_H,
            EFFECT,
            13,
            ci,
            ring=True,
        )

    root_props = (
        prop_s("name", "penrose", "      ")
        + prop_frame(0, 0, CANVAS_W, CANVAS_H, "      ")
        + prop_color(CHASSIS, "      ")
    )
    root = node("penrose", "GROUP", root_props, "", "  ", children)
    return "<?xml version='1.0' encoding='UTF-8'?>\n<lexml version='6'>\n" + root + "</lexml>\n"


def check(xml_text: str) -> None:
    """Fail loudly if the layout is not well formed or an action lost its pair.

    Guards the two defects this generator exists to prevent: an action that is not
    a real BUTTON, and a caption overlay that can steal the button's touch.
    """
    root = ET.fromstring(xml_text)

    def prop_of(node_el: ET.Element, key: str) -> str | None:
        for p in node_el.findall("./properties/property"):
            if (p.findtext("key") or "").strip() == key:
                return (p.findtext("value") or "").strip()
        return None

    def frame_of(node_el: ET.Element) -> tuple[str, ...]:
        for p in node_el.findall("./properties/property"):
            if (p.findtext("key") or "").strip() == "frame":
                return tuple((p.find("value").findtext(k) or "") for k in ("x", "y", "w", "h"))
        raise AssertionError("node has no frame")

    nodes = {prop_of(n, "name"): n for n in root.iter("node")}
    ids = [n.get("ID") for n in root.iter("node")]
    assert len(ids) == len(set(ids)), "duplicate node IDs"

    expected = ["nav1", "reset"] + [f"push{i}" for i in range(1, COLS * ROWS + 1)]
    for name in expected:
        button = nodes.get(name)
        cap = nodes.get(f"{name}_cap")
        assert button is not None and button.get("type") == "BUTTON", f"{name}: not a BUTTON"
        assert cap is not None and cap.get("type") == "TEXT", f"{name}: missing TEXT caption"
        assert prop_of(button, "interactive") == "1", f"{name}: button not interactive"
        assert prop_of(button, "background") == "1", f"{name}: pressed fill would be invisible"
        assert prop_of(button, "buttonType") == "0", f"{name}: not Momentary"
        assert prop_of(cap, "interactive") == "0", f"{name}_cap: overlay would steal the touch"
        assert frame_of(button) == frame_of(cap), f"{name}: caption frame does not match"
        text = cap.findtext("./values/value/default") or ""
        assert text.strip(), f"{name}_cap: empty caption"
        trigger = button.find("./messages/osc/triggers/trigger")
        assert trigger is not None, f"{name}: no send trigger"
        assert (trigger.findtext("var") or "").strip() == "x", f"{name}: trigger is not x"
        assert (trigger.findtext("condition") or "").strip() == "RISE", f"{name}: not press-only"

    # Each fader must carry a script that names an existing readout label, or the
    # value silently stops updating while the fader still works.
    for fader_name, readout_name in (("vscroll1", "val_brightness"), ("hscroll1", "val_period")):
        f = nodes.get(fader_name)
        assert f is not None and f.get("type") == "FADER", f"{fader_name}: missing FADER"
        script = prop_of(f, "script") or ""
        assert script.strip(), f"{fader_name}: no readout script"
        assert readout_name in script, f"{fader_name}: script does not target {readout_name}"
        assert "onReceiveOSC" in script, f"{fader_name}: heartbeat would fight a drag"
        assert (f.findtext("./messages/osc/receive") or "") == "1", f"{fader_name}: will not follow the wall"
        label = nodes.get(readout_name)
        assert label is not None and label.get("type") == "LABEL", f"{readout_name}: missing LABEL"
        assert prop_of(label, "interactive") == "0", f"{readout_name}: readout must not be touchable"
        assert (label.findtext("./values/value/default") or "").strip(), f"{readout_name}: no initial text"

    # Every action whose state the wall reports must have a live ring that is lit only
    # by an inbound message, sits strictly outside the button, and cannot take a touch.
    ringed = ["reset"] + [f"push{i}" for i in range(1, COLS * ROWS + 1)]
    assert nodes.get("nav1_ring") is None, "nav1 has no reported state to ring"
    for name in ringed:
        ring = nodes.get(f"{name}_ring")
        assert ring is not None and ring.get("type") == "BOX", f"{name}: missing live ring"
        assert prop_of(ring, "interactive") == "0", f"{name}_ring: ring would steal the touch"
        assert prop_of(ring, "background") == "0", f"{name}_ring: would start lit"
        assert prop_of(ring, "visible") == "1", f"{name}_ring: hidden controls may not receive"
        bx, by, bw, bh = (int(v) for v in frame_of(nodes[name]))
        rx, ry, rw, rh = (int(v) for v in frame_of(ring))
        assert (rx, ry) == (bx - RING_WIDTH, by - RING_WIDTH), f"{name}_ring: not centred on the button"
        assert (rw, rh) == (bw + 2 * RING_WIDTH, bh + 2 * RING_WIDTH), f"{name}_ring: wrong inflation"
        osc = ring.find("./messages/osc")
        assert osc is not None and (osc.findtext("receive") or "") == "1", f"{name}_ring: never lights"
        assert (osc.findtext("send") or "") == "0", f"{name}_ring: a ring must not transmit"
        arg = osc.find("./arguments/partial")
        assert (arg.findtext("type") or "") == "PROPERTY", f"{name}_ring: argument is not a property"
        assert (arg.findtext("value") or "").strip() == "background", f"{name}_ring: wrong property"

    print(f"checked {len(expected)} actions: BUTTON + non-interactive TEXT caption, x RISE send")
    print(f"checked {len(ringed)} live rings: receive-only BOX driving background, outside the button")
    print("checked 2 faders: script-driven readout label present and targeted")


if __name__ == "__main__":
    xml_text = build()
    check(xml_text)
    OUT_PATH.write_text(xml_text, encoding="utf-8")
    print(f"wrote {OUT_PATH} ({OUT_PATH.stat().st_size} bytes)", file=sys.stderr)
