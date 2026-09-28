# Card File Converter

Windows Forms C# application targeting **.NET Framework 4.8** (the current project setting), with no third-party packages.

## Run

Open `CardFileConverter/bin/Release/CardFileConverter.exe` after building, or open `CardFileConverter.sln` in Visual Studio and press F5.

1. Select the **Audit conversion** or **Inline conversion** tab. Each tab keeps its own folders, options and results for the current session.
2. Select the input folder. All CSV files directly inside it are processed (not subfolders).
3. Choose the output folder; the default is `Converted` inside the input folder. Files are saved directly in the chosen folder, without Audit, Inline or bank subfolders.
4. Choose UTF-8 (default) or Windows-1252 for input/output. Unicode byte-order marks are detected when reading.
5. Choose **Auto-detect**, **KARTY**, **QNB**, or **RAYAN BANK** in the Bank profile dropdown.
6. Click **Convert files**. Each file reports its bank and success/failure independently. Existing output files are never overwritten. Use a fresh output folder for reruns.

The interface uses Segoe UI fonts, larger headings, separate workflow tabs, alternating table rows, status colors and batch progress. **Refresh files** rescans the selected folder. **Open output** opens the output folder in Windows Explorer.

**Clear** resets only the active conversion tab's folders, file queue, progress and options. It does not delete source files, generated files or saved history. Conversion controls are disabled during a batch; the tabs and history remain viewable.

## Conversion history

The **Conversion history** tab automatically records each completed file attempt, including failures. Entries contain the completion time (shown in local time), workflow, input/output locations, result, error summary, encoding and ordering setting. Source record contents and card data are not logged. History begins with conversions made in this version; earlier runs are not reconstructed.

- Filter by bank, Audit/Inline and Success/Failed, and select an entry to view full locations and settings. New records include the selected bank option and resolved bank. Older history has `Not recorded` for bank; unknown-bank failures also appear under this filter.
- History survives application restarts and is stored per Windows user in `%LOCALAPPDATA%\CardFileConverter\History`.
- **Open output folder** opens the selected entry's destination. **Open history folder** opens the saved XML logs.
- Each entry is written to its own file using a temporary write and atomic rename. Separate application instances do not overwrite one another's entries; use Refresh to see entries written elsewhere.
- The latest 1,000 saved entries are displayed. Older files remain on disk; no automatic deletion is performed.
- Unreadable entries are skipped with a notice. If saving fails, conversion results remain valid and the app warns that those history entries exist only for the current session.

## Conversion rules derived from supplied samples

### Separate bank profiles

| Bank | Audit | Inline |
| --- | --- | --- |
| KARTY | Configured and sample-tested | Configured and sample-tested |
| QNB | Configured and sample-tested | Not configured; needs input/expected-output samples |
| RAYAN BANK | Not configured; needs input/expected-output samples | Configured and sample-tested |

Both conversion tabs have independent bank selectors, defaulting to Auto-detect. Detection happens for each CSV during conversion and appears in the queue's Bank column. Mixed-bank folders can be processed in Auto mode. All output files go directly into the chosen output folder; bank profiles remain separate conversion rules and do not create additional folders.

Detection uses distinct CSV header signatures, never filenames, folder names, card-number prefixes or guessed bank names. KARTY uses kit/plastic/entity/embossed-filename columns; QNB uses GUID/payment-reference columns; RAYAN uses its operation/client/account/card-category/limit columns. Each detected or selected profile then validates the full sample column layout and order. Capitalization, spaces and underscores are normalized; `CARD #` and `CARD_NO` are accepted aliases.

Unknown or conflicting signatures, wrong manual selections, unsupported workflows, and missing/extra/reordered columns are rejected. Manual selection does not bypass profile validation. No generic fallback is used by the application's conversion tabs. Clear resets the active tab's bank selection to Auto-detect.

### Audit

- Requires `SN` and `D_Status`. Removes `D_Status`, maps the KARTY/QNB headers to the supplied output names, trims field whitespace, and writes correctly escaped CSV.
- Keeps only records whose `D_Status` equals `Passed` (ignoring case and surrounding whitespace). Failed, pending, blank and other statuses are excluded before duplicate removal.
- Keeps the first successful record per card number for KARTY and per **Payment Reference ID** for QNB. SN and other differing values do not make the same identifier a new card. Identifiers are trimmed but otherwise compared exactly; empty successful-record identifiers are rejected. Deduplication applies within each input file.
- Preserves the retained records' input order by default and renumbers SN from 1. Optional numeric sorting uses the original SN after filtering and duplicate removal. If no Passed records remain, the output contains only the audit header.
- Produces `Audit_<CSV stem>_<yyyy_MM_dd_HH_mm_ss>.csv`. KARTY removes a leading `Inline_Converted_` from the input stem before constructing this name.

### Inline

- Requires `CARD #` or `CARD_NO`. Reads `.txt` and extensionless embossing files from the CSV folder.
- KARTY prefers `Converted_<CSV stem>` or `<CSV stem>`, with or without `.txt`. Otherwise it requires exactly one file containing all CSV card keys. Ambiguous matches fail rather than selecting an arbitrary file.
- RAYAN combines matching card records from multiple `.txt` or extensionless embossing files beside one CSV, including when a filename-matched file contains only part of the batch. Identical repeated embossing lines are collapsed; differing lines for the same card are rejected. All CSV cards must have a match. Unrelated card records are ignored, and output follows the CSV row order.
- Matches the CSV card number against the embossing line's first pipe-delimited field. Preserves the embossing line verbatim, including spaces and tabs; appends all CSV values separated by pipes, in CSV row order. Additional embossing records are not exported.
- Rejects missing/duplicate keys and CSV values containing pipes or embedded newlines because the supplied inline format has no escaping convention.
- Produces one `Inline_Converted_<CSV stem>.csv` per CSV for both KARTY and RAYAN. The extension changes, while the existing **pipe-delimited content** is preserved; it is not converted into comma-separated fields. Inline carrier files have no `D_Status`; passed-only filtering applies to audit inputs that carry this status.

Inputs are unchanged. Conversion runs in the background while the window remains responsive. Outputs contain the original card data, so use an appropriate local destination. Errors do not print card values.

## Error handling

- File-level failures are recorded in conversion history and the batch continues with remaining files. Setup errors are shown in the status area; conversion controls are restored after a failed batch.
- Messages distinguish output collisions, locked files, missing files/folders, access denial, invalid paths, encoding failures and disk-full errors.
- Empty/malformed CSVs, missing records, duplicate/mapped column collisions, ambiguous card columns and invalid SN sort values are rejected before output is created.
- CSV input streams are explicitly disposed even if parser initialization fails. Output/history temporary-file cleanup cannot replace the original I/O error; a cleanup failure is logged and may leave a `.tmp` file for inspection.
- Invalid history records (including null entries and invalid paths) are skipped. A history update failure does not change a completed conversion's result or stop later files.
- Unexpected startup/UI errors show a restart message and close the application instead of continuing in an uncertain state. Unhandled background errors are logged on a best-effort basis. Resource exhaustion and other fatal runtime errors are not treated as ordinary file failures.
- Diagnostic logs are stored under `%LOCALAPPDATA%\CardFileConverter\Logs`. They contain UTC time, operation, exception type, error code and stack trace; exception messages and source record contents are excluded. If the log location is unavailable, logging does not throw another error.

## Build and validation

Install Visual Studio / Build Tools with .NET desktop development and the **.NET Framework 4.8 targeting pack**:

```powershell
.\build.ps1
.\test.ps1
```

The updated application is built against the installed .NET Framework 4.8 targeting pack. The project and App.config already specified 4.8 when the UI enhancement began; that setting was preserved.

The test runner compares all six supplied expected outputs (three inline, three audit) and covers CSV quoting/multiline parsing, ordering, missing/duplicate keys, ambiguous pairing, malformed records, output collision protection, and temporary-file cleanup. Sample files remain in the sibling bank directories and are not copied into the project. Tests create synthetic temporary data and delete only their own uniquely named temporary folder.

UI/history checks also run synthetic conversions, reopen the form, verify Clear isolation, check successful and failed history entries, and exercise corrupt logs and storage failures. Synthetic UI test files and tab screenshots are retained under `TestResults` for inspection, separate from the user's actual history.

Bank-profile tests compare both automatic and manual selection against all six expected outputs, reject incompatible schemas and unsupported workflows, and verify mixed-bank batches, direct output placement without subfolders, dropdown independence, bank filtering and legacy history compatibility.
