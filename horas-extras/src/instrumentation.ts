export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { ensureAdminAccount } = await import("@/lib/bootstrap-admin");
    await ensureAdminAccount();
  }
}
