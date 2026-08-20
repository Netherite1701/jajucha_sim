# 2026 competition defaults

The supplied 2026 PDFs are authoritative for the competition course and sign
artwork. If older project code disagrees with those PDFs, this implementation
uses the PDFs. A value absent from all four PDFs is explicitly marked
`비공식 연습값`.

## Shipped stages

- `Courses/2026_preliminary.json` is the first-run default.
- `Courses/2026_final.json` is selectable from the runtime scenario panel.
- The last selected stage is restored on later launches.
- Both stages occupy 5.4 × 9.9 m and use 41 panels of 900 × 900 mm.
- Required inventory: A9, B2, C1, D5, F2, G1, H1, I1, J5, K3, L1,
  M2, N1, O1, P4, Q2.

The official assembly drawing is sampled onto one 5 cm mask. That mask drives
road/line judgement, while `track_surface.png` supplies the sensor-camera and
observer image. This keeps visual geometry and scoring geometry aligned.

Preliminary checkpoint order is start, S curve, right-angle corner, U tunnel,
straight hill, hill exit, zigzag, obstacle section, curve, finish. Final order
is start, S tunnel, right-angle corner, U-turn, corner hill, zigzag, obstacle
section, curve, finish.

## Structures and print artwork

- Hill: one 900 × 900 × 100 mm flat block between two slope blocks. The stored
  path height profile is 0/10/10/0 cm; preliminary is straight and final is curved.
- Tunnel: 220 mm high, 390 mm opening, roof sides 260 mm and 98 mm. Path points
  describe the preliminary U and final S shapes. An opaque black interior mask
  hides printed lane markings from sensor cameras.
- A4/B4 artwork is extracted at print scale for the four-red-lamp start board,
  yellow flag, PIT barrier, and dynamic obstacle. The source PDFs are preserved
  under `docs/reference/2026/`.

## Start and additional mission

The start signal lights one red lamp every 1.5 seconds. After all four lamps
are lit, a seeded random delay of 3–6 seconds is recorded. All lamps then turn
off and the one-second buzzer/release event starts. Movement before release is
a false start.

Five exterior straight candidates are named `candidate_1` through
`candidate_5`. Fixed mode requires the user to choose mission type and
candidate. Random mode chooses both per run and records the seed and resolved
selection. The Start Run button remains disabled until the initial mission
configuration is valid.

Yellow flag mode injects two sensors exactly 30 cm apart and measures
`speed = 30 cm / elapsed time`. Dynamic obstacle mode places the obstacle at
run start; after vehicle approach it waits and then exits the road.

## 비공식 연습값

These defaults are configurable and are not represented as official rules:

- speed limit 20 cm/s;
- obstacle wait 3 seconds and exit movement 1 second;
- base score 100;
- line contact, course departure, and collision: 5 points each;
- false start, mission failure, and timeout: 10 points each.

Result JSON records stage, resolved mission, candidate, random seed, actual
start hold, false start, measured mission speed, dynamic-obstacle collision,
mission pass/fail, and practice score. It also contains
`practiceValueLabel: "비공식 연습값"` and `practiceValuesOfficial: false`.
