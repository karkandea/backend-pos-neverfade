# NeverFade public demo conversion events

This is **demo-only**. The endpoint returns 404 unless `DemoMode:Enabled=true`.

- Event intake: POST `/api/demo/events`, anonymous allowlisted JSON only, rate-limited, max 2 KB.
- No IP address, name, phone, email, full URL, or free-form payload is stored.
- `session_id` is a random per-tab UUID, **not** merchant identity.
- Events persist across the three-hour demo data reset. Records older than 90 days are purged in bounded batches.
- Read metrics through privileged PostgreSQL administration, **not** the public demo owner token.
- Categories: `food_beverage`, `general_retail`, `fashion_retail`, `laundry`, `salon_barbershop`.
- Events: `category_selected`, `demo_mode_selected`, `demo_started`, `scenario_step_completed`, `scenario_completed`, `conversion_cta_clicked`.
- `mode`: `guided`, `free` (optional until mode selection).
- `step`: an allowlisted workflow step; CTA destinations `pricing`, `merchant_login` or `contact` (the latter is reserved until a real sales channel is configured).

Example funnel query (last 30 days):

```sql
SELECT "BusinessType", "Mode", "EventName",
       COUNT(*) AS events,
       COUNT(DISTINCT "SessionId") AS sessions
FROM demo_conversion_events
WHERE "CreatedAt" >= NOW() - INTERVAL '30 days'
GROUP BY "BusinessType", "Mode", "EventName"
ORDER BY "BusinessType", "Mode", "EventName";
```

Do not treat click counts as a conversion rate or sale. A sales conversion requires separate verified lead/customer data. Record outcome on the real merchant onboarding process when available.
