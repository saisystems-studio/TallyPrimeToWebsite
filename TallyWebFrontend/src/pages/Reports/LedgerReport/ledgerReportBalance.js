export function formatTallySignedBalance(value) {
  const amount = Math.abs(Number(value || 0)).toLocaleString("en-IN", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
  return `${amount} ${Number(value || 0) < 0 ? "Dr" : "Cr"}`;
}
