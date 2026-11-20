import React, { useState } from 'react';
import { HelpCircle } from 'lucide-react';

interface InfoTipProps {
  text: string;
  align?: 'left' | 'center' | 'right';
}

interface ParsedTip {
  summary: string;
  bullets: { label?: string; body: string }[];
}

function parseTipText(raw: string): ParsedTip {
  // Split into sentences/sections: first sentence is summary, rest become clean bullet items
  const normalized = raw.trim();
  const firstDot = normalized.indexOf('. ');

  let summary = normalized;
  let rest = '';
  if (firstDot > 0 && firstDot < normalized.length - 2) {
    summary = normalized.slice(0, firstDot + 1).trim();
    rest = normalized.slice(firstDot + 2).trim();
  }

  const bullets: { label?: string; body: string }[] = [];
  if (rest) {
    // Split on semicolons or explicit newlines into readable bullet rows
    const parts = rest
      .split(/\s*;\s*|\n+/)
      .map(s => s.trim())
      .filter(Boolean);

    for (const part of parts) {
      const colonIdx = part.indexOf(':');
      if (colonIdx > 0 && colonIdx <= 26) {
        bullets.push({
          label: part.slice(0, colonIdx).trim(),
          body: part.slice(colonIdx + 1).trim()
        });
      } else {
        bullets.push({ body: part });
      }
    }
  }

  return { summary, bullets };
}

export function InfoTip({ text, align = 'center' }: InfoTipProps) {
  const [open, setOpen] = useState(false);

  const alignClass =
    align === 'left'
      ? 'left-0'
      : align === 'right'
        ? 'right-0'
        : 'left-1/2 -translate-x-1/2';

  const arrowClass =
    align === 'left'
      ? 'left-2.5'
      : align === 'right'
        ? 'right-2.5'
        : 'left-1/2 -translate-x-1/2';

  const { summary, bullets } = parseTipText(text);

  return (
    <span
      className="relative inline-flex items-center"
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      <button
        type="button"
        aria-label={text}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onClick={e => {
          e.stopPropagation();
          setOpen(prev => !prev);
        }}
        className="inline-flex h-4 w-4 cursor-pointer items-center justify-center rounded-full text-zinc-400 transition-colors hover:text-emerald-400 focus-visible:text-emerald-400 focus-visible:outline-1 focus-visible:outline-emerald-400"
      >
        <HelpCircle className="h-3.5 w-3.5" />
      </button>

      {open && (
        <span
          role="tooltip"
          className={`pointer-events-none absolute bottom-full z-50 mb-2.5 block w-80 rounded-xl border border-zinc-600/90 bg-[#0d0d11]/98 px-4 py-3 text-left text-[13px] font-normal leading-relaxed text-zinc-200 shadow-[0_14px_36px_rgba(0,0,0,0.92)] backdrop-blur-md ${alignClass}`}
        >
          <span className="block font-medium text-white">{summary}</span>

          {bullets.length > 0 && (
            <span className="mt-2 block space-y-1.5 border-t border-zinc-800 pt-2">
              {bullets.map((b, i) => (
                <span key={i} className="flex items-start gap-2 text-[12.5px] leading-snug text-zinc-300">
                  <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-emerald-400" />
                  <span>
                    {b.label && (
                      <strong className="font-semibold text-emerald-300">{b.label}: </strong>
                    )}
                    {b.body}
                  </span>
                </span>
              ))}
            </span>
          )}

          <span
            className={`absolute top-full h-2 w-2 -translate-y-1 rotate-45 border-r border-b border-zinc-600/90 bg-[#0d0d11] ${arrowClass}`}
          />
        </span>
      )}
    </span>
  );
}
