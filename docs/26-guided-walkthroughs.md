# Hover-only guided walkthroughs — local debug build

## Try it

1. Focus the app you want help with and leave the pointer on that monitor.
2. Press Ctrl+Shift+Space, enable **Ask about my screen**, and ask
   "Show me how to open settings" or "Walk me through changing this option."
3. Whisper selects one currently accessible control, re-reads its live identity
   and bounds, highlights it and moves the pointer to its center. You click it.
4. The guide observes one real mouse-down/up on that target, without consuming
   or generating input. It waits for two stable, changed accessibility snapshots,
   then selects the next step automatically. There is no normal Next button.
   Refresh appears only if a control or a stable app change cannot be verified.
5. **Stop** or Esc cancels the turn, closes the card and clears the highlight.

Voice guidance uses the same path when screen sharing is enabled in voice settings
and the transcribed request is a how-to/guide request. Actual microphone behavior
still needs manual testing.

## Grounding and authority boundaries

- Screen consent is required; accessibility reading is bounded and skips
  password/edit controls and editable values.
- At most 24 enabled controls from the selected app/monitor are listed to the
  model. Labels are untrusted data. The model may select only a supplied target ID;
  it cannot supply cursor coordinates or clicks.
- Before moving the pointer, the desktop re-reads controls and checks runtime
  identity, label, uniqueness, positive bounds, monitor containment, unchanged
  foreground app and turn cancellation. A changed app or stale target blocks hover.
- The physical monitor mapping is used for guide highlights, independent of
  image-preview scaling and negative monitor origins.
- A uniquely visible control explicitly named in the goal can be selected
  directly, avoiding model latency. Later steps exclude already clicked labels.
  Checkbox state is supplied to the model rather than blindly selecting a toggle.
  Ambiguous steps use the model. No match means no invented target.
- This build never clicks, types, changes settings, launches apps or bypasses UAC
  on your behalf. Actual target clicks advance the walkthrough. Click observation
  ignores injected input, unrelated foreground clicks, long presses and drags out
  of the target. It runs only during an active grounded guide step, is single-use,
  is disposed between steps and on cancellation, and never logs mouse coordinates.
  App context is re-read after a matching click, not continuously in the background.

## Current limitations

Some apps, games, web canvases and custom menus do not expose usable accessibility
controls. In that case Whisper explains that it cannot point safely and asks you
to open the relevant app/menu manually. It does not guess image coordinates.
Pop-up menus and newly opened windows need real-app testing. The guide is bound
to the original monitor; move an app back there if it opens elsewhere.
Complex model-selected steps can still be slower than directly named controls.
The model's chosen step and completion claim remain suggestions, not proof that
the overall workflow is correct. A click plus changed screen context is not proof
of success: the next model step must inspect the current controls/state. Clicked
labels and a bounded step log reduce loops. Slow or inaccessible updates expose
Refresh rather than blindly repeating the previous step. No full cross-application
automation or exact HeyClicky parity is claimed. CPU-only visual interpretation
is still slow; no hosted provider receives screen data implicitly.

## Verification

The dedicated WPF fixture verifies a live Settings button identity/physical bounds
and excludes editable/password values. Pure tests reject stale identities, changed
labels, ambiguous matches and off-monitor geometry. Brain tests reject unknown
model target IDs, malformed guide JSON, model-supplied coordinates, duplicate
controls and guidance without screen consent. An actual local Qwen request selected
the supplied Settings target correctly. User-driven multi-step app testing remains
required before claiming reliable walkthroughs everywhere.
