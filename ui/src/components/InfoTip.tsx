import React, { useState } from 'react';
import { HelpCircle } from 'lucide-react';

interface InfoTipProps {
  text: string;
  align?: 'left' | 'center' | 'right';
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
        className="inline-flex h-4 w-4 cursor-pointer items-center justify-center rounded-full text-zinc-500 transition-colors hover:text-emerald-400 focus-visible:text-emerald-400 focus-visible:outline-1 focus-visible:outline-emerald-400"
      >
        <HelpCircle className="h-3.5 w-3.5" />
      </button>

      {open && (
        <span
          role="tooltip"
          className={`pointer-events-none absolute bottom-full z-50 mb-2 w-64 rounded-xl border border-zinc-700/90 bg-zinc-950/95 px-3 py-2 text-[11px] font-normal leading-relaxed text-zinc-200 shadow-[0_10px_30px_rgba(0,0,0,0.85)] backdrop-blur-md ${alignClass}`}
        >
          {text}
          <span
            className={`absolute top-full h-2 w-2 -translate-y-1 rotate-45 border-r border-b border-zinc-700/90 bg-zinc-950 ${arrowClass}`}
          />
        </span>
      )}
    </span>
  );
}
