# Sixty, exploded (Hairline reference)

A line-drawing reference for the stage: the app's 61-key ANSI 60% build in its
five layers (tray case, PCB, plate, switches, keycaps), drawn with
[Hairline](https://github.com/lucasmarkes/hairline)'s `hairline-create` skill.

Open `hairline-sixty.html` in a browser. Move across to open the stack; move
down to pick a layer, which takes the bright edge and goes to the read-out
(`03 · plate`). The slider is the gap.

- `sixty.js` is the figure, the only file to edit.
- `hairline-sixty.html` is built from it: `node <skill>/build.mjs sixty.js`.
- `hairline-sixty-look.png` is the skill's eight-picture look sheet.

The built page embeds Hairline's kernel and bench unchanged. Hairline is MIT
licensed, copyright (c) 2026 Lucas Marques.
