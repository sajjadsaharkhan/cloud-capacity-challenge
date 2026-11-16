#!/usr/bin/env bash
# Walks through every outcome against a running `make up`, printing each request and response.
set -u
BASE="${BASE_URL:-http://localhost:8080}"
RUN="$(date +%s)"   # keeps idempotency keys and client references unique per run

pretty() { if command -v jq >/dev/null; then jq -c .; else cat; fi; }
call() { # title customer actor-kind method path [json-body] ; extra headers via $EXTRA
  local title=$1 customer=$2 kind=$3 method=$4 path=$5 body=${6:-}
  echo; echo "── $title"; echo "   $method $path ${body:+$body}"
  curl -s -i -X "$method" "$BASE$path" \
    -H "X-Customer-Id: $customer" -H "X-Actor-Kind: $kind" -H "X-Actor-Id: demo-$kind" \
    -H "Content-Type: application/json" ${EXTRA:-} ${body:+-d "$body"} \
    | awk 'BEGIN{b=0} /^HTTP/{print "   " $0 > "/dev/stderr"} /^(Retry-After|Idempotent-Replayed|Location):/{print "   " $0 > "/dev/stderr"} /^\r?$/{b=1; next} b{print}' | pretty
}
id_of() { sed -n 's/.*"data":{"id":"\([^"]*\)".*/\1/p' | head -1; }
key() { echo "-H Idempotency-Key:demo-$RUN-$1"; }

echo "Capacity demo against $BASE"

call "1. Capacity levels, as a customer" demo-medium CUSTOMER GET /v1/capacity
call "2. Fit-check: would this be accepted? (advisory)" demo-medium CUSTOMER POST /v1/capacity/fit-check \
  '{"product":"vm-standard","region":"ir-thr","lines":[{"resourceClass":"Vcpu","quantity":4000},{"resourceClass":"RamMb","quantity":8192}]}'

EXTRA=$(key submit1) call "3. Submit a multi-line reservation: all lines held, or none" demo-medium CUSTOMER POST /v1/reservations \
  "{\"product\":\"vm-standard\",\"region\":\"ir-thr\",\"lines\":[{\"resourceClass\":\"Vcpu\",\"quantity\":4000},{\"resourceClass\":\"RamMb\",\"quantity\":8192},{\"resourceClass\":\"Ipv4\",\"quantity\":1}],\"clientReference\":\"demo-$RUN\"}" \
  | tee /tmp/demo-submit.out
RID=$(id_of < /tmp/demo-submit.out)
[ -z "$RID" ] && RID=$(grep -o '"id":"[^"]*"' /tmp/demo-submit.out | head -1 | cut -d'"' -f4)

EXTRA=$(key submit1) call "4. Retry with the same key: same answer, never a second reservation" demo-medium CUSTOMER POST /v1/reservations \
  "{\"product\":\"vm-standard\",\"region\":\"ir-thr\",\"lines\":[{\"resourceClass\":\"Vcpu\",\"quantity\":4000},{\"resourceClass\":\"RamMb\",\"quantity\":8192},{\"resourceClass\":\"Ipv4\",\"quantity\":1}],\"clientReference\":\"demo-$RUN\"}"
EXTRA=$(key submit1) call "5. Same key, different body: refused" demo-medium CUSTOMER POST /v1/reservations \
  '{"product":"vm-standard","region":"ir-thr","lines":[{"resourceClass":"Vcpu","quantity":1000}]}'
EXTRA=$(key dup) call "6. New key, same client reference: refused, no duplicate" demo-medium CUSTOMER POST /v1/reservations \
  "{\"product\":\"vm-standard\",\"region\":\"ir-thr\",\"lines\":[{\"resourceClass\":\"Vcpu\",\"quantity\":1000}],\"clientReference\":\"demo-$RUN\"}"

call "7. Track it" demo-medium CUSTOMER GET "/v1/reservations/$RID"
EXTRA=$(key extend) call "8. Extend the lease once" demo-medium CUSTOMER POST "/v1/reservations/$RID/extend"
EXTRA=$(key extend2) call "9. Extend again: refused" demo-medium CUSTOMER POST "/v1/reservations/$RID/extend"
EXTRA=$(key confirm) call "10. Confirm: held capacity becomes allocated" demo-medium CUSTOMER POST "/v1/reservations/$RID/confirm"
EXTRA=$(key cancel) call "11. Cancel a confirmed reservation: refused" demo-medium CUSTOMER POST "/v1/reservations/$RID/cancel"
EXTRA=$(key relcust) call "12. A customer may not release allocated capacity" demo-medium CUSTOMER POST "/v1/reservations/$RID/release" '{"reason":"ServiceDeleted"}'
EXTRA=$(key release) call "13. The product service releases it" demo-medium SERVICE POST "/v1/reservations/$RID/release" '{"reason":"ServiceDeleted"}'

EXTRA=$(key c1) call "14. Hold, then cancel: capacity returns at once" demo-medium CUSTOMER POST /v1/reservations \
  '{"product":"vm-standard","region":"ir-thr","lines":[{"resourceClass":"Vcpu","quantity":2000}]}' | tee /tmp/demo-c.out
CID=$(grep -o '"id":"[^"]*"' /tmp/demo-c.out | head -1 | cut -d'"' -f4)
EXTRA=$(key c2) call "    cancel" demo-medium CUSTOMER POST "/v1/reservations/$CID/cancel"

EXTRA=$(key quota) call "15. QUOTA_EXCEEDED: demo-small may have 16000 millicores" demo-small CUSTOMER POST /v1/reservations \
  '{"product":"vm-standard","region":"ir-thr","lines":[{"resourceClass":"Vcpu","quantity":64000}]}'
EXTRA=$(key bad) call "16. A quantity that breaks the step size" demo-medium CUSTOMER POST /v1/reservations \
  '{"product":"vm-standard","region":"ir-thr","lines":[{"resourceClass":"Vcpu","quantity":123}]}'
EXTRA=$(key gpu0) call "17. A GPU request without a GPU contract (default quota 0)" nobody CUSTOMER POST /v1/reservations \
  '{"product":"vm-gpu","region":"ir-thr","lines":[{"resourceClass":"Gpu","quantity":1}]}'

echo; echo "── Shortage codes, on a tiny IPv4 pool"
ADMIN='-H X-Actor-Kind:ADMIN -H X-Actor-Id:demo-admin -H Content-Type:application/json'
IPPOOL=$(curl -s "$BASE/v1/capacity?detail=true&region=ir-tbz" -H "X-Actor-Kind: SERVICE" -H "X-Actor-Id: demo" \
  | grep -o '"poolId":[0-9]*,"zone":null,"hostRef":null,"resourceClass":"Ipv4"' | head -1 | grep -o '[0-9]*' | head -1)
curl -s -X PUT "$BASE/v1/admin/pools/$IPPOOL/inventories/Ipv4" $ADMIN -H "Idempotency-Key: demo-$RUN-inv" \
  -d '{"total":20,"reserved":0,"allocationRatio":1,"minUnit":1,"stepSize":1,"maxUnit":32}' >/dev/null
curl -s -X PUT "$BASE/v1/admin/quotas/demo-large/Ipv4/ir-tbz" $ADMIN -H "Idempotency-Key: demo-$RUN-q" -d '{"limit":100}' >/dev/null
EXTRA=$(key ip1) call "18. Take 16 of the 20 addresses" demo-large CUSTOMER POST /v1/reservations \
  '{"product":"public-ip","region":"ir-tbz","lines":[{"resourceClass":"Ipv4","quantity":16}]}' | tee /tmp/demo-ip.out
IPID=$(grep -o '"id":"[^"]*"' /tmp/demo-ip.out | head -1 | cut -d'"' -f4)
EXTRA=$(key ip2) call "19. CAPACITY_TEMPORARILY_UNAVAILABLE: short only because of an open hold (Retry-After)" demo-large CUSTOMER POST /v1/reservations \
  '{"product":"public-ip","region":"ir-tbz","lines":[{"resourceClass":"Ipv4","quantity":8}]}'
EXTRA=$(key ip3) call "20. EXCEEDS_REGISTERED_CAPACITY: more than the pool can ever give (422)" demo-large CUSTOMER POST /v1/reservations \
  '{"product":"public-ip","region":"ir-tbz","lines":[{"resourceClass":"Ipv4","quantity":24}]}'
EXTRA=$(key ip4) call "21. Opt-in wait: 202 Waiting, then the sweeper promotes it when capacity returns" demo-large CUSTOMER POST /v1/reservations \
  '{"product":"public-ip","region":"ir-tbz","lines":[{"resourceClass":"Ipv4","quantity":8}],"onShortage":"Wait"}'
EXTRA=$(key ip5) call "    cancel the first hold" demo-large CUSTOMER POST "/v1/reservations/$IPID/cancel"
sleep 3
call "    the waiting request is now Held" demo-large CUSTOMER GET "/v1/reservations?product=public-ip&region=ir-tbz&status=Held"

call "22. Another customer cannot see these: 404" demo-small CUSTOMER GET "/v1/reservations/$RID"
call "23. Reconciliation: counters against the ledger (drift must be 0)" x ADMIN GET "/v1/admin/reconciliation?refresh=true"
EXTRA=$(key cleanup) call "24. Clean up: an operator cancels the customer's open reservations, then restores the pool" x ADMIN POST \
  /v1/admin/customers/demo-large/cancel-open
curl -s -X PUT "$BASE/v1/admin/pools/$IPPOOL/inventories/Ipv4" $ADMIN -H "Idempotency-Key: demo-$RUN-restore" \
  -d '{"total":1024,"reserved":100,"allocationRatio":1,"minUnit":1,"stepSize":1,"maxUnit":16}' >/dev/null
echo; echo "Done."
