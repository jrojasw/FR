export function BrandHeader() {
  return (
    <div className="mx-auto flex w-fit flex-col items-end">
      <span
        className="font-normal"
        style={{ fontSize: "7rem", lineHeight: 1, color: "#555555", fontFamily: "'Times New Roman', Times, serif" }}
      >
        Elon
      </span>
      <span className="-mt-1 text-xs text-slate-500">
        By <span style={{ color: "#2CA8DE" }}>CPY</span>
        <span style={{ color: "#E87033" }}>NOS</span>
      </span>
    </div>
  );
}
