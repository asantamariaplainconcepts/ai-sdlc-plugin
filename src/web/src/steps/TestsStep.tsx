import { useCallback, useEffect, useState } from "react";

/**
 * The Tests step: launch the review step's agent and record what it left.
 *
 * One button runs the step's declared prompt with `claude --print` in the worktree, held to
 * completion — no streaming, no chat. The run list is the record: exit code AND is_error (the
 * agent failing its own task exits 0 — both facts are drawn), cost with its absence stated
 * rather than drawn as zero, and the transcript located by rule or named absent.
 */

type Run = {
  sessionId: string;
  step: string | null;
  prompt: string | null;
  startingCommit: string | null;
  startedAt: string;
  finishedAt: string | null;
  exitCode: number | null;
  isError: boolean | null;
  costUsd: number | null;
  numTurns: number | null;
  durationMs: number | null;
  transcriptPath: string | null;
  transcriptLocated: boolean;
  resultSummary: string | null;
  problem: string | null;
};

type Runs = { path: string; problem: string | null; runs: Run[] };

const box: React.CSSProperties = { padding: "6px 12px", borderRadius: "var(--radius)", border: "1px solid var(--border)", background: "var(--card)", color: "var(--foreground)", cursor: "pointer" };

const row: React.CSSProperties = { display: "flex", flexWrap: "wrap", columnGap: 10, rowGap: 2, alignItems: "baseline", fontSize: 12, padding: "6px 0", borderTop: "1px solid var(--border)" };

function locate(r: Run): string {
  if (r.costUsd !== null) return `$${Number(r.costUsd).toFixed(2)}`;
  return "cost not given — the provider said nothing about it";
}

export function TestsStep({ path, hasPrompt }: { path: string; hasPrompt: boolean }) {
  const [runs, setRuns] = useState<Runs | null>(null);
  const [launching, setLaunching] = useState(false);
  const [launched, setLaunched] = useState<Run | null>(null);
  const [error, setError] = useState<string | null>(null);

  const list = useCallback(async () => {
    try {
      const response = await fetch(`/api/runs?path=${encodeURIComponent(path)}`);
      setRuns(response.ok ? ((await response.json()) as Runs) : null);
    } catch {
      setRuns(null);
    }
  }, [path]);

  useEffect(() => {
    void list();
  }, [list]);

  const launch = async () => {
    setLaunching(true);
    setError(null);
    try {
      const response = await fetch(`/api/runs?path=${encodeURIComponent(path)}&step=tests`, { method: "POST" });
      const body = (await response.json()) as { problem: string | null; run: Run | null };
      setLaunched(body.run);
      if (body.problem) setError(body.problem);
      await list();
    } catch (e) {
      setError(e instanceof Error ? e.message : "the launch itself failed");
    } finally {
      setLaunching(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <div style={{ display: "flex", gap: 8, alignItems: "baseline", flexWrap: "wrap" }}>
        <button type="button" onClick={() => void launch()} disabled={launching || !hasPrompt} style={box}>
          {launching ? "running — held to completion…" : "run the tests step"}
        </button>
        {!hasPrompt ? (
          <span style={{ fontSize: 12, color: "var(--muted-foreground)" }}>
            no prompt is declared for this step — add one under "prompts" in .harness/review.json
          </span>
        ) : null}
      </div>

      {error ? (
        <div role="note" style={{ fontSize: 12, color: "var(--bad)" }}>{error}</div>
      ) : null}

      {launched && !error ? (
        <div role="status" style={{ fontSize: 12, color: "var(--muted-foreground)" }}>
          run {launched.sessionId.slice(0, 8)} recorded against {launched.startingCommit?.slice(0, 8) ?? "no commit"}:
          exit {launched.exitCode ?? "never ran"}
          {launched.isError === null ? null : `, provider error: ${launched.isError}`}
        </div>
      ) : null}

      {runs?.problem ? (
        <div role="note" style={{ fontSize: 12, color: "var(--warn)" }}>{runs.problem}</div>
      ) : null}

      <div>
        {(runs?.runs ?? []).length === 0 && !runs?.problem ? (
          <p style={{ margin: 0, fontSize: 12 }}>No run has been recorded for this worktree yet.</p>
        ) : null}
        {(runs?.runs ?? []).map((r) => (
          <div key={r.sessionId} className="mono" style={row}>
            <span style={{ color: "var(--foreground)" }}>{r.startedAt.replace("T", " ").slice(0, 19)}</span>
            <span>session {r.sessionId.slice(0, 8)}</span>
            <span>from {r.startingCommit?.slice(0, 8) ?? "no commit"}</span>
            <span style={{ color: r.exitCode === 0 ? "var(--foreground)" : "var(--bad)" }}>
              exit {r.exitCode ?? "never ran"}
            </span>
            <span>provider error: {r.isError === null ? "not said" : r.isError ? "yes" : "no"}</span>
            <span>{locate(r)}</span>
            <span>
              {r.transcriptLocated ? `transcript at ${r.transcriptPath}` : "transcript absent — the derived path is not there"}
            </span>
            {r.resultSummary ? <span style={{ flexBasis: "100%", color: "var(--muted-foreground)" }}>{r.resultSummary.slice(0, 160)}</span> : null}
            {r.problem ? <span style={{ flexBasis: "100%", color: "var(--warn)" }}>{r.problem}</span> : null}
          </div>
        ))}
      </div>
    </div>
  );
}
