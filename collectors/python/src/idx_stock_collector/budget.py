"""Provider-independent quota arithmetic only; never authorizes collection."""


def preflight(equities: int, benchmark: bool, daily_remaining: int,
              extra_balance: int, run_ceiling: int, reserve: int = 0,
              retries: int = 0, units_per_attempt: int = 1) -> dict:
    values = (equities, daily_remaining, extra_balance, run_ceiling, reserve,
              retries, units_per_attempt)
    if (any(type(v) is not int or v < 0 for v in values)
            or type(benchmark) is not bool or units_per_attempt < 1
            or equities < 1):
        raise ValueError("Nonnegative integer counters and a positive universe/cost required.")
    requests = equities + int(benchmark)
    attempts = requests * (1 + retries)
    maximum = attempts * units_per_attempt
    extra = max(0, maximum - daily_remaining)
    eligible = maximum <= run_ceiling and extra_balance - extra >= reserve
    return {"equities": equities, "benchmark": int(benchmark),
            "potential_requests": requests, "maximum_attempts": attempts,
            "maximum_units": maximum, "daily_remaining": daily_remaining,
            "extra_balance": extra_balance, "extra_consumption": extra,
            "run_ceiling": run_ceiling, "minimum_extra_reserve": reserve,
            "status": "ELIGIBLE" if eligible else "BUDGET_BLOCKED"}
