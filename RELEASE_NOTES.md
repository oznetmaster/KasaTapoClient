# KasaTapoClient 2.2.2

- When a SMART refresh batch times out, retry its reads individually. The device instance keeps using individual core reads on later polls, avoiding a repeated timeout. Caller cancellation still stops the refresh.
- Discover outlets during the first refresh of SMART strips such as the Tapo P300. If a later page is missing, keep the previously published outlet list and report the refresh failure.

This patch preserves the public API, runtime dependencies, and support for .NET 10 and .NET Framework 4.7.2. The release also includes a separate portable symbol package.

Validation passed on the workstation with 180 offline tests per framework and on Crestron processor Mono with all 180 offline tests. The timeout regressions used simulated SMART responses. [Validation record](https://github.com/oznetmaster/KasaTapoClient/blob/v2.2.2/release-validation/v2.2.2.json)

Upstream reference [python-kasa #1759](https://github.com/python-kasa/python-kasa/pull/1759).
