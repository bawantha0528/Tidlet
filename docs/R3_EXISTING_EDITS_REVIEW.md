# Review of Network/Battery edits found during the pause

30 October 2026. These newer workspace edits are preserved. They are not part of the frozen R2 correction binary measured under bin/r2-correction. No R3 acceptance or resource pass is implied by the current source building.

Items to resolve before treating the current Network popup as complete:

- WindowsPopupSource currently adds instantaneous byte rates into session totals. Rates must be integrated using elapsed monotonic time, or totals derived from validated cumulative adapter deltas; sample scheduling must not change the answer.
- Interface identity currently chooses the first managed active interface, which may differ from the physical adapters included in the traffic aggregate. Show the actual measured adapters, avoid virtual fallback, and distinguish aggregate from per-interface details.
- GetIsNetworkAvailable reports local network availability, not tested internet reachability. Do not label it Internet status. Gateway ICMP does not establish internet reachability either.
- Gateway Ping currently runs automatically on popup reads. Add explicit opt-in configuration, cancellation/bounded timing, at least three samples and documented jitter math. Keep outbound disabled by default. Public IP stays off.
- Network values need explicit unavailable states rather than zero; null address/latency rows should hide or explain absence. The generic percent chart is not a traffic chart; implement mirrored byte-rate history with a suitable scale.
- Battery EstimatedRemaining is not an estimate of charging completion; do not relabel it Time to full. Do not display inferred adapter power, voltage or charging state as reported facts. Validate capacity and health independently and hide invalid values.

R2 CPU/RAM resource/lifecycle gates remain the prerequisite for expanding presentation. Then review and correct these preserved edits with pure math/failure tests, followed by live read-only and full resource trials. No helper, new package, admin rights or system changes.
