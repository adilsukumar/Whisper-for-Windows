# Excel routing and current limitations

The previous implementation conflated two distinct questions:

- Explain a sheet, formula, or cell: needs sheet content, not just ribbon labels.
- Point to a named control: needs a current actionable UI control and live bounds.

The word `how` or `settings` alone no longer activates a walkthrough. Explicit
navigation such as `Click Formulas` or `Open the View tab` still does. A control
label merely mentioned in a sentence is no longer enough for a direct click step.

For a screen-shared Excel explanation, the brain uses image analysis instead of
the fast ribbon-label-only route. Existing privacy rules still prohibit reading
editable values via accessibility APIs. Image analysis is local and can remain
slow; the old nine-second cutoff is not restored. A screenshot may not reveal a
full formula, hidden cells, or workbook structure: provide relevant cells/formula
text when needed. No Excel COM access or workbook files are read by this change.

Excel walkthroughs currently support explicitly named, uniquely visible controls.
For an unsupported task, arbitrary model-based ribbon selection is stopped rather
than presented as correct. This is a safety restriction, not a finished Excel
automation engine. Task-aware Excel workflows require exact reproduction cases
and verification of workbook selection, formulas, dialog state and resulting
changes. No Excel task success is claimed by the synthetic routing tests.

Other guide prompts now include the active app and visible labels alongside the
goal, actual clicks, and failed controls; these are untrusted context, not commands.
