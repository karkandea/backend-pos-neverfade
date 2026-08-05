#!/usr/bin/env python3

import json
import sys
from collections import defaultdict
from datetime import datetime, timedelta, timezone
from decimal import Decimal
from pathlib import Path
from zoneinfo import ZoneInfo

WIB = ZoneInfo("Asia/Jakarta")
PERIODS = ("harian", "mingguan", "bulanan", "tahunan")
LABELS = ("Sen", "Sel", "Rab", "Kam", "Jum", "Sab", "Min")

root = Path(sys.argv[1])
result_file = Path(sys.argv[2])

checks = []
details = []

def load(name):
    with (root / name).open(
        "r",
        encoding="utf-8",
    ) as file:
        return json.load(
            file,
            parse_float=Decimal,
            parse_int=Decimal,
        )

def parse_datetime(value):
    text = str(value)

    if text.endswith("Z"):
        text = text[:-1] + "+00:00"

    parsed = datetime.fromisoformat(text)

    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)

    return parsed.astimezone(WIB)

def period_start(period, now_wib):
    today = now_wib.date()

    if period == "harian":
        return today

    if period == "mingguan":
        return today - timedelta(days=6)

    if period == "bulanan":
        return today.replace(day=1)

    return today.replace(month=1, day=1)

def add_check(label, passed, detail=""):
    checks.append((label, passed))

    if detail:
        details.append(f"{label}: {detail}")

transactions = load("transactions.json")
now_wib = datetime.now(WIB)

parsed_transactions = []

for transaction in transactions:
    parsed_transactions.append(
        (
            transaction,
            parse_datetime(transaction["tanggal"]),
        )
    )

for period in PERIODS:
    start_date = period_start(period, now_wib)

    selected = [
        transaction
        for transaction, tanggal in parsed_transactions
        if tanggal.date() >= start_date
    ]

    expected_omzet = sum(
        (
            Decimal(str(transaction.get("total", 0)))
            for transaction in selected
        ),
        Decimal("0"),
    )

    expected_count = len(selected)

    expected_customers = len(
        {
            transaction.get("customerId")
            for transaction in selected
            if transaction.get("customerId") is not None
        }
    )

    expected_avg = (
        Decimal("0")
        if expected_count == 0
        else expected_omzet / Decimal(expected_count)
    )

    actual = load(f"summary-{period}.json")

    actual_omzet = Decimal(str(actual.get("omzet", 0)))
    actual_count = int(actual.get("transaksi", 0))
    actual_customers = int(actual.get("pelanggan", 0))
    actual_avg = Decimal(str(actual.get("avg", 0)))

    add_check(
        f"Summary {period} omzet",
        actual_omzet == expected_omzet,
        f"expected={expected_omzet}, actual={actual_omzet}",
    )

    add_check(
        f"Summary {period} transaction count",
        actual_count == expected_count,
        f"expected={expected_count}, actual={actual_count}",
    )

    add_check(
        f"Summary {period} unique customers",
        actual_customers == expected_customers,
        f"expected={expected_customers}, actual={actual_customers}",
    )

    add_check(
        f"Summary {period} average",
        abs(actual_avg - expected_avg) < Decimal("0.01"),
        f"expected={expected_avg}, actual={actual_avg}",
    )

invalid_summary = load("summary-invalid.json")
daily_summary = load("summary-harian.json")

add_check(
    "Invalid summary period defaults to daily",
    invalid_summary == daily_summary,
)

chart = load("chart.json")
today = now_wib.date()
start_date = today - timedelta(days=6)

expected_chart = []

for offset in range(7):
    day = start_date + timedelta(days=offset)

    total = sum(
        (
            Decimal(str(transaction.get("total", 0)))
            for transaction, tanggal in parsed_transactions
            if tanggal.date() == day
        ),
        Decimal("0"),
    )

    expected_chart.append(
        {
            "date": day.isoformat(),
            "label": LABELS[day.weekday()],
            "total": total,
        }
    )

add_check(
    "Chart contains exactly seven days",
    len(chart) == 7,
    f"actual length={len(chart)}",
)

chart_matches = True

for index, expected in enumerate(expected_chart):
    if index >= len(chart):
        chart_matches = False
        break

    actual = chart[index]

    same = (
        actual.get("date") == expected["date"]
        and actual.get("label") == expected["label"]
        and Decimal(str(actual.get("total", 0))) == expected["total"]
    )

    if not same:
        chart_matches = False

        details.append(
            "Chart mismatch "
            f"date={expected['date']}, "
            f"expected total={expected['total']}, "
            f"actual date={actual.get('date')}, "
            f"actual total={actual.get('total')}"
        )

add_check(
    "Chart totals follow Asia/Jakarta calendar dates",
    chart_matches,
)

for period in PERIODS:
    start_date = period_start(period, now_wib)
    aggregates = defaultdict(
        lambda: {
            "qty": 0,
            "revenue": Decimal("0"),
        }
    )

    for transaction, tanggal in parsed_transactions:
        if tanggal.date() < start_date:
            continue

        for item in transaction.get("items", []):
            name = item.get("nama", "")
            aggregates[name]["qty"] += int(item.get("qty", 0))
            aggregates[name]["revenue"] += Decimal(
                str(item.get("subtotal", 0))
            )

    actual_top = load(f"top-{period}.json")

    all_values_valid = True
    descending = True
    previous_qty = None

    for item in actual_top:
        name = item.get("nama", "")
        qty = int(item.get("qty", 0))
        revenue = Decimal(str(item.get("revenue", 0)))
        expected = aggregates.get(name)

        if expected is None:
            all_values_valid = False
            details.append(
                f"Top {period}: unexpected product {name}"
            )
            continue

        if (
            qty != expected["qty"]
            or revenue != expected["revenue"]
        ):
            all_values_valid = False
            details.append(
                f"Top {period}: {name}, "
                f"expected qty/revenue="
                f"{expected['qty']}/{expected['revenue']}, "
                f"actual={qty}/{revenue}"
            )

        if previous_qty is not None and qty > previous_qty:
            descending = False

        previous_qty = qty

    add_check(
        f"Top products {period} maximum ten rows",
        len(actual_top) <= 10,
        f"actual length={len(actual_top)}",
    )

    add_check(
        f"Top products {period} aggregate values",
        all_values_valid,
    )

    add_check(
        f"Top products {period} sorted descending",
        descending,
    )

invalid_top = load("top-invalid.json")
daily_top = load("top-harian.json")

add_check(
    "Invalid top-products period defaults to daily",
    invalid_top == daily_top,
)

with result_file.open(
    "w",
    encoding="utf-8",
) as file:
    for label, passed in checks:
        state = "PASS" if passed else "FAIL"
        file.write(f"{state}\t{label}\n")

    if details:
        file.write("\nDETAILS\n")

        for detail in details:
            file.write(f"{detail}\n")

for label, passed in checks:
    state = "PASS" if passed else "FAIL"
    print(f"{state}\t{label}")

raise SystemExit(
    1 if any(not passed for _, passed in checks) else 0
)
