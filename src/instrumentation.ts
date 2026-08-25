export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { startPaymentReminderScheduler } = await import("@/lib/payment-reminders");
    startPaymentReminderScheduler();
  }
}
