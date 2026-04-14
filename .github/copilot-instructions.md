# Copilot Instructions

## Project Guidelines
- Steering document rule: When making a change to one page, apply that same change to all pages in the project.
- Steering document hierarchy: A project should have one or many milestones; each milestone should have one or many Test Plans and/or Test Runs; a Test Plan should contain Test Runs (not "Entries"). The UI should label them as "Test Runs" when displayed on the PlanDetail page.
- Automatically order every grid or table alphabetically by name.
- Every table and grid must have clickable, sortable column headers. Use the `sortable` CSS class on each `<th>` (except Actions or checkbox columns) and set `data-sort-type` to `number`, `date`, or `text` (default). The shared `wwwroot/js/sortable.js` script handles the sort behavior automatically.
- Brand colors: teal (#00A3AD) and black (#1A1A1A). Use CSS custom properties (--brand-teal, --brand-dark) in site.css via Bootstrap overrides rather than hardcoding colors in Razor files.