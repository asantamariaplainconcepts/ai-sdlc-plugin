import { useEffect, useState } from "react";

/**
 * The Code step: the change's diff as a diff, drawn by hand — no diff library, the epic's pin.
 *
 * Added and removed are separated by marker and tint both (never color alone), the gutters carry
 * old/new numbers, and the two-hundred-line cap is a visible cut naming how much is hidden: a
 * diff over the cap is cut, not dumped. A diff nobody can ask for (no trunk) is a sentence,
 * never an empty diff.
 */

type Line = { kind: "context" | "added" | "removed"; oldLine: number | null; newLine: number | null; text: string };

type Hunk = { header: string; lines: Line[] };

type File = { path: string; renamedFrom: string | null; isBinary: boolean; hunks: Hunk[] };

type Code = {
  path: string;
  problem: string | null;
  basis: string | null;
  cutAfter: number | null;
  hiddenLinesCount: number;
  files: File[];
};

const gutter: React.CSSProperties = {
  width: 40,
  flex: "none",
  textAlign: "right",
  paddingRight: 8,
  color: "var(--muted-foreground)",
  userSelect: "none",
};

const tints = { added: "rgba(46, 160, 67, 0.14)", removed: "rgba(248, 81, 73, 0.14)", context: "transparent", marks: { added: "+", removed: "−", context: "" } } as const;

export function CodeStep({ path }: { path: string }) {
  const [code, setCode] = useState<Code | null>(null);
  const [chosen, setChosen] = useState<string | null>(null);

  useEffect(() => {
    setCode(null);
    setChosen(null);
    fetch(`/api/code?path=${encodeURIComponent(path)}`)
      .then((r) => (r.ok ? (r.json() as Promise<Code>) : Promise.reject(new Error(`the server said ${r.status}`))))
      .then(setCode, () => setCode({ path, problem: "the diff could not be read", basis: null, cutAfter: null, hiddenLinesCount: 0, files: [] }));
  }, [path]);

  // The selection follows the list: a file the branch reverted cannot stay open.
  const files = code?.files ?? [];
  const file = files.find((f) => f.path === chosen) ?? files[0];

  if (!code) {
    return <p style={{ margin: 0 }}>reading the diff…</p>;
  }

  if (code.problem) {
    return <p style={{ margin: 0 }}>{code.problem}</p>;
  }

  if (files.length === 0) {
    return (
      <p style={{ margin: 0 }}>
        the branch changes nothing against its merge base — {code.basis ? "the base is " + code.basis.slice(0, 7) : "no base"}
      </p>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10, minHeight: 0 }}>
      <div role="group" aria-label="the files of the change" style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
        {files.map((entry) => (
          <button
            key={entry.path}
            type="button"
            aria-pressed={entry.path === file?.path}
            onClick={() => setChosen(entry.path)}
            className="mono"
            title={entry.renamedFrom ? `${entry.renamedFrom} → ${entry.path}` : entry.path}
            style={{
              padding: "4px 10px",
              borderRadius: "var(--radius)",
              border: `1px solid ${entry.path === file?.path ? "var(--primary)" : "var(--border)"}`,
              background: "var(--card)",
              color: entry.path === file?.path ? "var(--foreground)" : "var(--muted-foreground)",
              fontSize: 11,
              cursor: "pointer",
              maxWidth: "100%",
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
            }}
          >
            {entry.path}
          </button>
        ))}
      </div>

      <div style={{ borderTop: "1px solid var(--border)", paddingTop: 10, overflow: "auto" }}>
        {file?.renamedFrom ? (
          <p className="mono" style={{ margin: "0 0 6px", fontSize: 11, color: "var(--muted-foreground)" }}>
            renamed from {file.renamedFrom}
          </p>
        ) : null}

        {file?.isBinary ? (
          <p style={{ margin: 0 }}>this file is binary — its diff has no lines to read</p>
        ) : file && file.hunks.length === 0 ? (
          <p style={{ margin: 0 }}>no hunks to read — the file's change carries no line rows</p>
        ) : (
          file?.hunks.map((hunk) => (
            <div key={hunk.header} style={{ borderBottom: "1px solid var(--border)" }}>
              <div className="mono" style={{ padding: "4px 12px", background: "var(--muted)", fontSize: 11, color: "var(--muted-foreground)" }}>
                {hunk.header}
              </div>
              {hunk.lines.map((line, index) => (
                <div
                  key={index}
                  className="mono"
                  style={{ display: "flex", fontSize: 11.5, lineHeight: 1.55, background: tints[line.kind] }}
                >
                  <span style={gutter}>{line.oldLine ?? ""}</span>
                  <span style={gutter}>{line.newLine ?? ""}</span>
                  <span style={{ width: 14, flex: "none", textAlign: "center", color: "var(--muted-foreground)" }}>
                    {tints.marks[line.kind]}
                  </span>
                  <span style={{ whiteSpace: "pre", paddingRight: 12, minWidth: 0, overflowWrap: "anywhere" }}>{line.text}</span>
                </div>
              ))}
            </div>
          ))
        )}

        {code.cutAfter != null || code.hiddenLinesCount > 0 ? (
          <p role="note" style={{ margin: "10px 0 0", padding: "6px 10px", borderRadius: "var(--radius)", border: "1px dashed var(--border)", fontSize: 12, color: "var(--warn)" }}>
            the diff is cut here — {code.hiddenLinesCount} more lines are not shown (the step presents two hundred)
          </p>
        ) : null}
      </div>
    </div>
  );
}
