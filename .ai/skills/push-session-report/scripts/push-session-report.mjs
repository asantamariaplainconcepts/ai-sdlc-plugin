#!/usr/bin/env node
// Push one telemetry record for a command's phase to .telemetry/executions.jsonl.
// See .ai/skills/push-session-report/SKILL.md for what calls this and why.

import { readFileSync, appendFileSync, mkdirSync, existsSync, readdirSync, statSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';

function parseArgs(argv) {
  const out = { status: 'ok' };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--change') out.change = argv[++i];
    else if (a === '--phase') out.phase = argv[++i];
    else if (a === '--status') out.status = argv[++i];
    else if (a === '--note') out.note = argv[++i];
    else throw new Error(`Unknown argument: ${a}`);
  }
  return out;
}

function fail(message) {
  process.stderr.write(`push-session-report: ${message}\n`);
  process.exit(1);
}

// Approximate USD-per-million-token rates. Not sourced from live billing — a best-effort
// estimate computed from the transcript's own token counts, for a retro-log figure that is
// "close and honest about being an estimate" rather than exact. Update as pricing changes;
// an unlisted model reports tokens with cost marked unavailable rather than a guessed rate.
const PRICING_PER_MILLION_USD = {
  'claude-opus-5': { input: 15, output: 75, cacheWrite: 18.75, cacheRead: 1.5 },
  'claude-sonnet-5': { input: 3, output: 15, cacheWrite: 3.75, cacheRead: 0.3 },
  'claude-haiku-4-5-20251001': { input: 1, output: 5, cacheWrite: 1.25, cacheRead: 0.1 },
};

function findTranscriptPath(sessionId) {
  const projectsDir = path.join(homedir(), '.claude', 'projects');
  if (!sessionId || !existsSync(projectsDir)) return null;
  const target = `${sessionId}.jsonl`;
  const stack = [projectsDir];
  while (stack.length) {
    const dir = stack.pop();
    let entries;
    try {
      entries = readdirSync(dir, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) stack.push(full);
      else if (entry.isFile() && entry.name === target) return full;
    }
  }
  return null;
}

function summarizeTranscript(transcriptPath) {
  const lines = readFileSync(transcriptPath, 'utf8').split('\n').filter(Boolean);
  const totals = { input_tokens: 0, output_tokens: 0, cache_creation_tokens: 0, cache_read_tokens: 0 };
  let costUsd = 0;
  let costKnown = true;
  let firstTs = null;
  let lastTs = null;

  for (const line of lines) {
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (entry.timestamp) {
      const ts = Date.parse(entry.timestamp);
      if (!Number.isNaN(ts)) {
        if (firstTs === null || ts < firstTs) firstTs = ts;
        if (lastTs === null || ts > lastTs) lastTs = ts;
      }
    }
    if (entry.type !== 'assistant') continue;
    const usage = entry.message?.usage;
    if (!usage) continue;

    const input = usage.input_tokens ?? 0;
    const output = usage.output_tokens ?? 0;
    const cacheWrite = usage.cache_creation_input_tokens ?? 0;
    const cacheRead = usage.cache_read_input_tokens ?? 0;
    totals.input_tokens += input;
    totals.output_tokens += output;
    totals.cache_creation_tokens += cacheWrite;
    totals.cache_read_tokens += cacheRead;

    const rate = PRICING_PER_MILLION_USD[entry.message?.model];
    if (rate) {
      costUsd +=
        (input * rate.input + output * rate.output + cacheWrite * rate.cacheWrite + cacheRead * rate.cacheRead) /
        1_000_000;
    } else {
      costKnown = false;
    }
  }

  const durationMs = firstTs !== null && lastTs !== null ? lastTs - firstTs : null;
  return {
    tokens: totals,
    duration_ms: durationMs,
    cost_usd_estimated: costKnown ? Number(costUsd.toFixed(4)) : null,
  };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.change) fail('--change is required');
  if (!args.phase) fail('--phase is required');
  if (!['ok', 'degraded', 'failed'].includes(args.status)) {
    fail(`--status must be ok, degraded or failed (got "${args.status}")`);
  }
  if (args.status === 'ok' && args.note) {
    fail('a --note is not allowed with --status ok — a finding must not be buried in a record that reads as fine');
  }
  if (args.status !== 'ok' && !args.note) {
    fail(`--status ${args.status} requires a --note describing what happened`);
  }

  const sessionId = process.env.CLAUDE_CODE_SESSION_ID || null;
  const transcriptPath = findTranscriptPath(sessionId);

  // Every push re-reads the whole transcript, so two pushes from one session overlap: the second
  // is a superset of the first, not the slice between them. The session id is what lets a reader
  // take one figure per session instead of adding the same tokens twice.
  const record = {
    timestamp: new Date().toISOString(),
    change: args.change,
    phase: args.phase,
    session_id: sessionId,
    status: args.status,
    note: args.note ?? null,
  };

  if (transcriptPath) {
    record.usage = summarizeTranscript(transcriptPath);
  } else {
    record.usage = { unavailable: true, reason: sessionId ? 'no transcript file found for this session' : 'no session id in this runtime' };
  }

  const telemetryDir = path.join(process.cwd(), '.telemetry');
  mkdirSync(telemetryDir, { recursive: true });
  appendFileSync(path.join(telemetryDir, 'executions.jsonl'), `${JSON.stringify(record)}\n`);

  const usageNote = record.usage.unavailable
    ? `no usage figure (${record.usage.reason})`
    : `${record.usage.tokens.input_tokens + record.usage.tokens.output_tokens} tokens` +
      (record.usage.cost_usd_estimated !== null ? `, ~$${record.usage.cost_usd_estimated}` : ', cost unavailable for this model');
  process.stdout.write(`Pushed ${args.status} record for ${args.change}/${args.phase}: ${usageNote}\n`);
}

main();
