export const meta = {
  name: 'implement-plan',
  description: 'Plan a change, implement it, adversarially trim sprawl, then review and test',
  whenToUse: 'Feature or refactor work in ImageMagitek/TileShop. Pass the task description as args (string or {task}).',
  phases: [
    { title: 'Plan', detail: 'explore the code and produce a concrete implementation plan' },
    { title: 'Implement', detail: 'apply the plan and get a clean build' },
    { title: 'Simplify', detail: 'adversarial critic attacks sprawl; implementer applies accepted cuts' },
    { title: 'Review', detail: 'correctness review + build/test run in parallel' },
    { title: 'Fix', detail: 'verify findings, fix confirmed ones, re-test' },
  ],
}

const task = typeof args === 'string' ? args : args?.task
if (!task) throw new Error('Pass the task description as args, e.g. args: "Add X to the palette editor"')

const MAX_SIMPLIFY_ROUNDS = 2

const BUILD_RULES = `Build rules: before building TileShop.UI, run \`Stop-Process -Name TileShop.UI -Force -ErrorAction SilentlyContinue\` (a running app breaks the copy step). Build with \`dotnet build TileShop.UI\\TileShop.UI.csproj -c Debug -v q -nologo\`. If ImageMagitek core or ImageMagitek.Services changed, also run \`dotnet test ImageMagitek.UnitTests\`. Use PowerShell. Do not commit, stash, or reset git state.`

const DIFF_CMD = 'Inspect the current change with `git status --porcelain` and `git diff` (also read any untracked files it lists).'

const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string', description: 'One paragraph describing the approach' },
    steps: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          file: { type: 'string' },
          change: { type: 'string' },
        },
        required: ['file', 'change'],
      },
    },
    reuse: { type: 'array', items: { type: 'string' }, description: 'Existing types/helpers the change should reuse instead of adding new ones' },
    testStrategy: { type: 'string', description: 'Unit tests to add/run and any UI verification needed' },
    touchesUI: { type: 'boolean' },
    risks: { type: 'array', items: { type: 'string' } },
  },
  required: ['summary', 'steps', 'reuse', 'testStrategy', 'touchesUI', 'risks'],
}

const IMPL_SCHEMA = {
  type: 'object',
  properties: {
    filesChanged: { type: 'array', items: { type: 'string' } },
    buildSucceeded: { type: 'boolean' },
    testsPassed: { type: 'boolean', description: 'true if tests passed or were not applicable' },
    notes: { type: 'string', description: 'Deviations from the plan and anything left undone' },
  },
  required: ['filesChanged', 'buildSucceeded', 'testsPassed', 'notes'],
}

const CRITIQUE_SCHEMA = {
  type: 'object',
  properties: {
    cuts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          file: { type: 'string' },
          line: { type: 'integer' },
          kind: { type: 'string', enum: ['unneeded-abstraction', 'duplication', 'reinvented-helper', 'dead-code', 'over-defensive', 'comment-noise', 'scope-creep', 'naming', 'other'] },
          problem: { type: 'string' },
          fix: { type: 'string', description: 'The concrete simpler form' },
        },
        required: ['file', 'kind', 'problem', 'fix'],
      },
    },
  },
  required: ['cuts'],
}

const FINDINGS_SCHEMA = {
  type: 'object',
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          file: { type: 'string' },
          line: { type: 'integer' },
          severity: { type: 'string', enum: ['high', 'medium', 'low'] },
          issue: { type: 'string' },
          scenario: { type: 'string', description: 'Concrete input/state that triggers the bug or failure' },
        },
        required: ['file', 'severity', 'issue', 'scenario'],
      },
    },
  },
  required: ['findings'],
}

const TEST_SCHEMA = {
  type: 'object',
  properties: {
    buildSucceeded: { type: 'boolean' },
    testsPassed: { type: 'boolean' },
    testsAdded: { type: 'array', items: { type: 'string' } },
    uiVerified: { type: 'string', description: 'What was checked in the running app, or "n/a"' },
    failures: FINDINGS_SCHEMA.properties.findings,
  },
  required: ['buildSucceeded', 'testsPassed', 'testsAdded', 'uiVerified', 'failures'],
}

const VERDICT_SCHEMA = {
  type: 'object',
  properties: {
    confirmed: FINDINGS_SCHEMA.properties.findings,
    rejected: { type: 'array', items: { type: 'string' }, description: 'Each rejected finding with the reason' },
  },
  required: ['confirmed', 'rejected'],
}

const fmtPlan = p => `${p.summary}\n\nSteps:\n${p.steps.map((s, i) => `${i + 1}. ${s.file}: ${s.change}`).join('\n')}\n\nReuse: ${p.reuse.join('; ') || 'none'}\nTest strategy: ${p.testStrategy}\nRisks: ${p.risks.join('; ') || 'none'}`
const fmtList = (items, f) => items.map((x, i) => `${i + 1}. ${f(x)}`).join('\n')

phase('Plan')
const plan = await agent(
  `Plan the implementation of this task in the ImageMagitek repo. Do not edit any files.

Task: ${task}

Explore the relevant code first. Prefer the smallest change that fits the existing architecture: extend existing types and reuse existing helpers, services, converters and styles rather than adding parallel ones. List every existing piece the change should reuse. Keep the plan to what the task needs; note but do not plan adjacent cleanups.`,
  { label: 'planner', schema: PLAN_SCHEMA },
)
log(`Plan: ${plan.steps.length} steps, touchesUI=${plan.touchesUI}`)

phase('Implement')
const impl = await agent(
  `Implement this plan in the working tree.

Task: ${task}

Plan:
${fmtPlan(plan)}

Follow the plan; if it is wrong somewhere, deviate minimally and say why in notes. Match the surrounding code's style and the comment rules in CLAUDE.md. Add or update unit tests per the test strategy when core logic changes. ${BUILD_RULES} Iterate until the build is clean.`,
  { label: 'implementer', schema: IMPL_SCHEMA },
)
if (!impl.buildSucceeded) log(`Implementer could not get a clean build: ${impl.notes}`)

phase('Simplify')
const simplifyLog = []
for (let round = 1; round <= MAX_SIMPLIFY_ROUNDS; round++) {
  const critique = await agent(
    `You are an adversarial reviewer whose only goal is to make this change smaller and easier for a human to maintain. ${DIFF_CMD}

Task it implements: ${task}

Attack every added line. Look for: abstractions, interfaces, or parameters with a single use; code duplicating an existing helper elsewhere in the repo (search for it); dead or speculative code; defensive checks for states that cannot occur; comments that restate code or describe the change; work beyond what the task needs; unclear names. Each cut must keep behavior identical and must name the concrete simpler form. Do not report correctness bugs or style nits a formatter would fix. Return an empty list if the change is already lean; do not invent cuts. Do not edit files.`,
    { label: `critic:r${round}`, schema: CRITIQUE_SCHEMA, effort: 'high' },
  )
  if (!critique.cuts.length) {
    log(`Simplify round ${round}: nothing to cut`)
    break
  }
  log(`Simplify round ${round}: ${critique.cuts.length} proposed cuts`)
  const applied = await agent(
    `A critic proposed these simplifications to the current uncommitted change (${DIFF_CMD}):

${fmtList(critique.cuts, c => `[${c.kind}] ${c.file}${c.line ? ':' + c.line : ''} — ${c.problem} → ${c.fix}`)}

Apply each cut that keeps behavior identical and genuinely makes the code simpler. Reject cuts that would break behavior, hurt clarity, or fight the codebase's established patterns, and say why. ${BUILD_RULES} Finish with a clean build.`,
    { label: `simplifier:r${round}`, schema: IMPL_SCHEMA },
  )
  simplifyLog.push({ round, proposed: critique.cuts.length, notes: applied.notes, buildSucceeded: applied.buildSucceeded })
  if (critique.cuts.length <= 2) break
}

phase('Review')
const [review, tests] = await parallel([
  () => agent(
    `Review the current uncommitted change for correctness. ${DIFF_CMD} Do not edit files.

Task it implements: ${task}

Look for logic errors, broken edge cases (empty arrangers, palette index bounds, odd tile sizes, null data sources), MVVM binding mistakes, undo/redo or dirty-state gaps, resource/lifetime leaks, and missed callers of changed APIs. Report only issues with a concrete triggering scenario. Return an empty list if none.`,
    { label: 'reviewer', schema: FINDINGS_SCHEMA, effort: 'high' },
  ),
  () => agent(
    `Verify the current uncommitted change builds and is tested. ${DIFF_CMD}

Test strategy from the plan: ${plan.testStrategy}

${BUILD_RULES} If core logic changed and lacks coverage, add focused xUnit tests next to the existing ones in ImageMagitek.UnitTests. ${plan.touchesUI ? 'The change touches the UI: after a clean build, launch the app and verify it with the Avalonia DevTools MCP per the "Running and inspecting the UI" section of CLAUDE.md. Stop the app when done.' : 'UI verification is not needed.'} Do not modify non-test code; report failures instead.`,
    { label: 'tester', schema: TEST_SCHEMA },
  ),
])

const candidates = [...(review?.findings ?? []), ...(tests?.failures ?? [])]

phase('Fix')
let verdict = { confirmed: [], rejected: [] }
let fix = null
if (candidates.length) {
  verdict = await agent(
    `Try to refute each of these reported issues against the current uncommitted change (${DIFF_CMD}). Read the code and trace the scenario. Keep a finding only if you can confirm it is real; when uncertain, reject it. Do not edit files.

${fmtList(candidates, f => `[${f.severity}] ${f.file}${f.line ? ':' + f.line : ''} — ${f.issue} (scenario: ${f.scenario})`)}`,
    { label: 'verifier', schema: VERDICT_SCHEMA, effort: 'high' },
  )
  log(`${verdict.confirmed.length}/${candidates.length} findings confirmed`)
}
if (verdict.confirmed.length) {
  fix = await agent(
    `Fix these confirmed issues in the current uncommitted change with the smallest correct edits:

${fmtList(verdict.confirmed, f => `[${f.severity}] ${f.file}${f.line ? ':' + f.line : ''} — ${f.issue} (scenario: ${f.scenario})`)}

${BUILD_RULES} Finish with a clean build and passing tests.`,
    { label: 'fixer', schema: IMPL_SCHEMA },
  )
}

return {
  task,
  plan: fmtPlan(plan),
  implementation: impl,
  simplify: simplifyLog,
  tests,
  confirmedIssues: verdict.confirmed,
  rejectedIssues: verdict.rejected,
  fix,
  finalBuildSucceeded: fix ? fix.buildSucceeded : (tests?.buildSucceeded ?? false),
  finalTestsPassed: fix ? fix.testsPassed : (tests?.testsPassed ?? false),
}
