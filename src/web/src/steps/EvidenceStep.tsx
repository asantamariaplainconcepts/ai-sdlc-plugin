import { useEffect, useState } from "react";

/**
 * The Evidence step: the runs recorded against this worktree, read-only. A run was recorded
 * whatever happened — exit code AND provider error are two facts (a failed task still exits 0),
 * an absent cost is a sentence about the provider, never a free run. The trigger rides each row:
 * provenance is reviewable here, while the launch that produced it never branched on it (POC-05).
 *
 * No launch, no mutation: evidence is what the record already holds. Runs are launched from the
 * step that runs them; this step is where their trail is read after the work.
 */

type Run = {
  sessionId: string;
  step: string | null;
  startedAt: string;
  finishedAt: string | null;
  exitCode: number | null;
  isError: boolean | null;
  costUsd: number | null;
  transcriptPath: string | null;
  transcriptLocated: boolean;
  resultSummary: string | null;
  problem: string | null;
  trigger: string | null;
};

const row: React.CSSProperties = { display: "flex", flexWrap: "wrap", columnGap: 10, alignItems: "baseline", fontSize: 12, padding: "6px 0", borderTop: "1px solid var(--border)" };

export function EvidenceStep({ path }: { path: string }) {
  const [runs, setRuns] = useState<Run[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    let live = true;
    setRuns(null);
    setProblem(null);
    // a failed listing is a named failure, never stale rows over a fetch that did not answer
    fetch(`/api/runs?path=${encodeURIComponent(path)}`)
      .then(async (response) => {
        const body = (await response.json()) as { problem: string | null; runs: Run[] };
        if (!live) return;
        setRuns(body.runs);
        setProblem(body.problem);
      })
      .catch((e: unknown) => {
        if (!live) return;
        setRuns(null);
        setProblem(`the runs could not be read (${e instanceof Error ? e.message : "the server did not answer"})`);
      });
    return () => { live = false; };
  }, [path]);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      {problem ? <div role="note" style={{ fontSize: 12, color: "var(--warn)" }}>{problem}</div> : null}
      {runs?.length === 0 && !problem ? (
        <p style={{ margin: 0, fontSize: 12 }}>
          no run recorded yet — runs are launched from the step that runs them, and their record lands here
        </p>
      ) : null}
      {(runs ?? []).map((r) => (
        <div key={r.sessionId} className="mono" style={row}>
          <span style={{ color: "var(--foreground)" }}>{r.startedAt.replace("T", " ").slice(0, 19)}</span>
          <span>session {r.sessionId.slice(0, 8)}</span>
          <span>{r.step ?? "step not said"}</span>
          <span style={{ color: r.exitCode === 0 ? "var(--foreground)" : "var(--bad)" }}>exit {r.exitCode ?? "never ran"}</span>
          <span>provider error: {r.isError === null ? "not said" : r.isError ? "yes" : "no"}</span>
          <span>{r.costUsd === null ? "cost not given — the provider said nothing about it" : `$${Number(r.costUsd).toFixed(2)}`}</span>
          <span title={r.transcriptPath ?? undefined}>
            {r.transcriptLocated ? "transcript at its recorded path" : "transcript absent — the derived path is not there"}
          </span>
          <span>{r.finishedAt ? `finished ${r.finishedAt.replace("T", " ").slice(0, 19)}` : "not finished — or its finish was not recorded"}</span>
          <span>trigger: {r.trigger ?? "not said — the row predates the column"}</span>
          {r.problem ? <span style={{ flexBasis: "100%", color: "var(--warn)" }}>{r.problem}</span> : null}
          {r.resultSummary ? <span style={{ flexBasis: "100%", color: "var(--muted-foreground)" }}>{r.resultSummary.slice(0, 160)}</span> : null}
        </div>
      ))}
    </div>
  );
}
