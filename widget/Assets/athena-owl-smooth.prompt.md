# Coruja de Atena: animação com oito quadros por estado

Ferramenta: `image_gen`, modo integrado, fundo transparente. Referência: a primeira versão da coruja (`athena-owl.png`, no histórico do git).

Arquivo gerado sem modificações: `athena-owl-smooth.png`, 1774 × 887, oito colunas e quatro linhas. Estados: repouso, atividade, atenção e sono. O aplicativo alinha os pés, mantém o tamanho aparente da coruja e interpola as poses em memória.

Prompt usado:

> Production game animation asset, NOT a UI mockup. Expand the attached Athena owl sprite sheet from four frames per state to EIGHT frames per state, preserving exactly this owl's identity, rounded proportions, ivory/brown feather texture, muted bronze helmet and olive ornament, amber eyes, cute style. Create a new large LANDSCAPE 2:1 sheet with EXACTLY 8 equally spaced columns and 4 rows, 32 complete owl sprites total, ideally 3072x1536 or equivalent. TRUE transparent alpha background.
>
> Each cell is square, 8 columns left to right are consecutive animation frames, 4 rows top to bottom are states. Scale the owl identically in all cells, use generous transparent padding above the helmet and below the feet, all feet on the SAME local baseline. Wings must stay inside their cell even at maximum extension. The head, torso and helmet must NOT jump around between frames. NO cropping, NO sprite touching a neighbor. Crisp clean outline, no red/green matte fringes, no cast shadow.
>
> ROW 1 IDLE: 8 very small intermediate poses for a gentle breathing and blink loop. Frames 1,2,3 eyes open with subtle natural head movement; frame 4 eyelids half closing; frame 5 eyes closed for a blink; frame 6 eyelids half opening; frames 7,8 eyes open returning smoothly to exactly the same head pose as frame 1. No dramatic head rotation.
>
> ROW 2 ACTIVE WING FLAP: 8 distinct smoothly spaced wing positions, drawn as a complete seamless up-down cycle. Frames 1 folded, 2 slightly out, 3 half up, 4 nearly fully up, 5 fully up, 6 half down, 7 slightly out, 8 folded. Smaller angular steps than the reference's jerky four-pose animation. Keep face/body fixed in place and fixed scale.
>
> ROW 3 ATTENTIVE: 8 tiny intermediate head tilt poses, a gentle left to center to right to center loop, eyes alert and wings tucked, same torso/foot position. No extreme tilt and no angry face.
>
> ROW 4 SLEEP: 8 subtly breathing closed-eye poses with wings tucked around the body, tiny gradual compression/expansion, return smoothly to starting pose. No letters or Z symbols.
>
> This is specifically a frame expansion, NOT a character redesign. All 32 cells must be fully present, aligned regularly with enough blank space to crop each sprite. It must have exactly EIGHT owls across each of FOUR rows. Do not output the original 4x4 grid. No labels, text, grid lines, border, UI, black background, white background, or painted checkerboard. Transparent background only.
