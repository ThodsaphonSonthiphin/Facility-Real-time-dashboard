# Data contracts (the JSON that flows between the steps)

Three small JSON shapes connect the steps. Keep them stable — the bundled scripts
depend on these exact field names.

```
extract-findings        -> findings.json
classify-work-items     -> backlog_input.json   (consumed by create-backlog.cs)
ado-create-work-items   -> backlog_result.json  (consumed by tracking.py writeback)
```

The link between a source row and its created ticket is the **`key`** field — a stable
identifier for each finding (a row number, an existing ID column, or one you assign).
It is carried through all three files so ticket links can be written back to the right row.

---

## 1. `findings.json` — normalized findings (output of `extract-findings`)

```json
{
  "source": "Downloads/CRM_Portal_Naming_Audit.xlsx (sheet 'CRM-Portal Audit')",
  "keyColumn": "#",
  "findings": [
    {
      "key": "1",
      "section": "Service Group / Cargo Classification",
      "current": "Auto",
      "expected": "Automotive Cargo",
      "kind": "rename",
      "severity": "Critical",
      "status": "confirmed",
      "recommendation": "Rename to: Automotive Cargo",
      "notes": "'Auto' loses the word 'Cargo'; ambiguous with car brand."
    }
  ]
}
```

| field | required | notes |
|---|---|---|
| `key` | yes | Stable id per finding. Prefer an existing source column (row #, ID). Used for write-back. |
| `current` / `expected` | yes | Observed value vs the canonical/authoritative value. |
| `kind` | no | `rename` \| `disambiguation` \| `missing` \| `other`. Helps `classify-work-items` pick a type. |
| `severity` | no | `Critical` \| `High` \| `Medium` \| `Low`. Drives Priority + triage. |
| `status` | no | Free text (e.g. `confirmed`, `needs-review`). Useful triage filter. |
| `section` / `recommendation` / `notes` | no | Carried into the work-item description. |

For non-tabular input (a doc, pasted text) there may be no natural key — assign `"1","2",...`.
Write-back only applies when the source is a spreadsheet.

---

## 2. `backlog_input.json` — work items to create (output of `classify-work-items`)

```json
{
  "org": "Cartagena365",
  "project": "GlassHull",
  "parent": {
    "type": "Feature",
    "fields": {
      "System.Title": "CRM <-> Portal Naming Alignment",
      "System.Tags": "naming-audit; crm-portal",
      "Microsoft.VSTS.Common.Priority": 1
    }
  },
  "items": [
    {
      "key": "1",
      "type": "Bug",
      "fields": {
        "System.Title": "Portal label \"Auto\" should display CRM-canonical \"Automotive Cargo\"",
        "Microsoft.VSTS.Common.Priority": 1,
        "System.Tags": "naming-audit; crm-portal; review-confirmed",
        "Microsoft.VSTS.TCM.ReproSteps": "<b>Current:</b> Auto<br><b>Expected:</b> Automotive Cargo"
      },
      "estimate": {
        "hours": 2,
        "task": {
          "fields": {
            "System.Title": "Implement: rename \"Auto\" -> \"Automotive Cargo\"",
            "Microsoft.VSTS.Scheduling.OriginalEstimate": 2,
            "Microsoft.VSTS.Scheduling.RemainingWork": 2,
            "System.Description": "<b>Estimate breakdown</b><ul><li>Update dropdown label (UI): 1h</li><li>Verify Quote-description usage + test: 1h</li></ul><b>Total: 2h</b>"
          }
        }
      }
    }
  ]
}
```

- `org` / `project` are optional here — env vars `AZDO_ORG` / `AZDO_PROJECT` override them.
- `parent` is optional. If present, it is created first and every item is linked under it
  (`System.LinkTypes.Hierarchy-Reverse`). Use a `Feature` or `Epic`.
- `type` must be a valid work item type **for the target project's process** (see below).
- `fields` are raw ADO reference names. Strings or numbers. Common ones:
  - `System.Title` (required), `System.Tags` (`"a; b; c"`), `Microsoft.VSTS.Common.Priority` (1-4)
  - Bug body: `Microsoft.VSTS.TCM.ReproSteps`
  - Story/PBI body: `System.Description` + `Microsoft.VSTS.Common.AcceptanceCriteria`
  - Optional: `System.AreaPath`, `System.IterationPath` (or set `AZDO_AREA_PATH` env)
  - Optional: `System.AssignedTo` (a UPN/email, e.g. `name@cartagena.no`) — omit to leave
    unassigned. To assign the whole batch instead of per item, set the `AZDO_ASSIGNED_TO`
    env var at create time (it only applies to items that don't set their own `System.AssignedTo`).
- HTML fields (`ReproSteps`, `Description`) must be HTML — escape `& < >`.

### Estimates → a child Task (optional)

If an item has an `estimate` object, `create-backlog.cs` creates one **child Task** under that
item to hold the hour estimate:

- `estimate.hours` — total hours (informational / for roll-up).
- `estimate.task.fields` — the Task's fields:
  - `System.Title` — e.g. `Implement: <parent title>`
  - `Microsoft.VSTS.Scheduling.OriginalEstimate` + `Microsoft.VSTS.Scheduling.RemainingWork` —
    the hours (equal at creation; may be decimals like `1.5`)
  - `System.Description` — the **detailed breakdown** (HTML): each step + its hours, then the total
  - omit `System.AssignedTo` / `System.Tags` / area to inherit the run-wide `AZDO_ASSIGNED_TO`
    / `AZDO_AREA_PATH`, or set them per Task
- **Why a Task:** in Agile a **User Story has no hours field** (only Story Points), so the hour
  estimate lives where it is valid — a Task — uniformly under every Bug/Story. See `docs/adr/0001`.

### Work item types by process (pick types that exist on the target board)

| Process | "defect" | "new capability" | grouping parents | generic task |
|---|---|---|---|---|
| Agile | Bug | User Story | Feature, Epic | Task |
| Scrum | Bug | Product Backlog Item | Feature, Epic | Task |
| Basic | (none) | Issue | Epic | Task |
| CMMI | Bug | Requirement | Feature, Epic | Task |

---

## 3. `backlog_result.json` — what got created (output of `create-backlog.cs`)

```json
{
  "org": "Cartagena365",
  "project": "GlassHull",
  "parent": { "id": 6072, "url": "https://dev.azure.com/Cartagena365/.../_apis/wit/workItems/6072" },
  "items": [
    { "key": "1", "id": 6073, "url": "https://dev.azure.com/.../workItems/6073", "type": "Bug", "title": "..." }
  ]
}
```

`tracking.py writeback` matches each result `key` to the source's key column and writes
`Ticket ID` / `Ticket URL` / `WI State` / `Created`.
