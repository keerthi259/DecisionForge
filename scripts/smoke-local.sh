#!/usr/bin/env bash
set -euo pipefail

api_base_url="${DECISIONFORGE_API_URL:-http://localhost:5066}"
web_base_url="${DECISIONFORGE_WEB_URL:-http://localhost:5173}"
mailpit_base_url="${DECISIONFORGE_MAILPIT_URL:-http://localhost:8025}"
timeout_seconds="${DECISIONFORGE_SMOKE_TIMEOUT_SECONDS:-180}"

resolve_command() {
  local candidate
  for candidate in "$@"; do
    if command -v "${candidate}" >/dev/null 2>&1; then
      printf '%s' "${candidate}"
      return
    fi
  done
  return 1
}

curl_command="$(resolve_command curl.exe curl)" || {
  echo 'curl is required for the local smoke test.' >&2
  exit 1
}
curl_output_sink='/dev/null'
if [[ "${curl_command}" == *.exe ]]; then
  curl_output_sink='NUL'
fi

wait_for_url() {
  local url="$1"
  local deadline=$((SECONDS + timeout_seconds))
  local body

  until body="$("${curl_command}" --fail --silent --max-time 10 "${url}" 2>/dev/null)"; do
    if (( SECONDS >= deadline )); then
      echo "Timed out waiting for ${url}." >&2
      return 1
    fi
    sleep 2
  done

  printf '%s' "${body}"
}

frontend_body="$(wait_for_url "${web_base_url}")"
grep -q 'DecisionForge' <<<"${frontend_body}"
[[ "$(wait_for_url "${api_base_url}/health/live")" == 'Healthy' ]]
[[ "$(wait_for_url "${api_base_url}/health/ready")" == 'Healthy' ]]

version_body="$(wait_for_url "${api_base_url}/version")"
grep -q '"application":"DecisionForge.Api"' <<<"${version_body}"

proxy_headers="$("${curl_command}" --fail --silent --show-error --dump-header - --output "${curl_output_sink}" \
  --header 'X-Correlation-ID: phase-3-smoke' "${web_base_url}/health/live")"
grep -qi '^X-Correlation-ID: phase-3-smoke' <<<"${proxy_headers}"

wait_for_url "${mailpit_base_url}/api/v1/info" >/dev/null

echo "frontend: PASS (${web_base_url})"
echo "liveness: PASS (${api_base_url}/health/live)"
echo "readiness: PASS (${api_base_url}/health/ready)"
echo 'version: PASS'
echo 'same-origin proxy and correlation: PASS'
echo "mailpit: PASS (${mailpit_base_url})"
