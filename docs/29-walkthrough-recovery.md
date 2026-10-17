# Wrong or repeating walkthrough instructions

The old model prompt asked not to repeat steps, but there was no hard rejection
of repeat suggestions. Its structured input also lacked the clicked/failed
history. These are now enforced separately from model reasoning.

1. Clicked and explicitly failed labels are removed from eligible controls.
   Matching ignores letter case and repeated whitespace, even if the target id
   changes after navigation. The desktop independently rejects these repeats.
2. Goal, actual clicks, and failures are separate bounded fields in model input.
   Failure feedback is not recorded as a successful click.
3. Model-selected ids must exist in current accessibility controls. Instructions
   are rendered from the selected label, preventing prose that names a different
   control or invents later steps.
4. A model-only claim that the goal is finished is no longer accepted as proof.
   When no grounded next step is available, the guide stops pointing and asks for
   manual navigation or clarification; it does not retry the same action.
5. Use **Didn't work** on a suggested step. Whisper blocks that control for the
   rest of the walkthrough and reads the current app again. Starting a new
   question or using Stop clears this session history.

Limits: selecting an existing control does not prove it is appropriate for every
goal. The small local model can still choose the wrong *different* control.
Blocking by label is conservative: separate controls with the same label are
also blocked for this session. If no reliable alternative remains, stop and
describe the app, goal, instruction, and actual result rather than retrying
blindly. No click is executed by Whisper. Photo analysis and its previous long
timeout are unchanged; the removed nine-second deadline remains removed.

Regression checks cover changed ids, casing/spacing, failed and clicked history,
malformed history, fabricated instruction text, and unverified completion claims.
