#!/bin/bash
# Run OWASP Amass enum across multiple domains in parallel using Kubernetes engine pods
# Each engine pod handles one enumeration at a time; excess domains queue until a pod is free

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Defaults
NAMESPACE=""
SELECTOR="app.kubernetes.io/component=engine"
DOMAIN_FILE=""
DRY_RUN=false
DOMAINS=()

# Cleanup state
FIFO_PATH=""
FD3_OPEN=false

usage() {
    cat <<EOF
Usage: $0 [OPTIONS] [DOMAIN ...]

Run OWASP Amass enum across multiple domains in parallel using K8s engine pods.

Options:
  -f FILE       Read domains from file (one per line; blank lines and #comments skipped)
  -n NAMESPACE  Kubernetes namespace (default: current kubectl context)
  -l SELECTOR   Label selector for engine pods (default: app.kubernetes.io/component=engine)
  --dry-run     Show what would run without executing
  -h, --help    Show help

Examples:
  $0 example.com example.org
  $0 -f domains.txt
  $0 -n amass -f domains.txt extra-domain.com
  $0 --dry-run -f domains.txt
EOF
    exit 0
}

# --- Parse args ---

while [[ $# -gt 0 ]]; do
    case "$1" in
        -f)
            [[ $# -lt 2 ]] && { echo -e "${RED}Error: -f requires a file argument${NC}" >&2; exit 1; }
            DOMAIN_FILE="$2"
            shift 2
            ;;
        -n)
            [[ $# -lt 2 ]] && { echo -e "${RED}Error: -n requires a namespace argument${NC}" >&2; exit 1; }
            NAMESPACE="$2"
            shift 2
            ;;
        -l)
            [[ $# -lt 2 ]] && { echo -e "${RED}Error: -l requires a selector argument${NC}" >&2; exit 1; }
            SELECTOR="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        -*)
            echo -e "${RED}Error: unknown option '$1'${NC}" >&2
            echo "Run '$0 --help' for usage." >&2
            exit 1
            ;;
        *)
            DOMAINS+=("$1")
            shift
            ;;
    esac
done

# --- Load domains from file ---

if [[ -n "$DOMAIN_FILE" ]]; then
    if [[ ! -f "$DOMAIN_FILE" ]]; then
        echo -e "${RED}Error: domain file '$DOMAIN_FILE' not found${NC}" >&2
        exit 1
    fi
    while IFS= read -r line || [[ -n "$line" ]]; do
        # Trim whitespace
        line="${line#"${line%%[![:space:]]*}"}"
        line="${line%"${line##*[![:space:]]}"}"
        # Skip blank lines and comments
        [[ -z "$line" || "$line" == \#* ]] && continue
        DOMAINS+=("$line")
    done < "$DOMAIN_FILE"
fi

# --- Deduplicate domains ---

if [[ ${#DOMAINS[@]} -eq 0 ]]; then
    echo -e "${RED}Error: no domains specified${NC}" >&2
    echo "Provide domains as arguments or via -f FILE. Run '$0 --help' for usage." >&2
    exit 1
fi

declare -A SEEN_DOMAINS
UNIQUE_DOMAINS=()
for d in "${DOMAINS[@]}"; do
    if [[ -n "${SEEN_DOMAINS[$d]+x}" ]]; then
        echo -e "${YELLOW}Warning: duplicate domain '$d' — skipping${NC}"
    else
        SEEN_DOMAINS[$d]=1
        UNIQUE_DOMAINS+=("$d")
    fi
done
DOMAINS=("${UNIQUE_DOMAINS[@]}")

echo "=== Amass Parallel Domain Enumeration ==="
echo ""

# --- Prerequisite checks ---

echo "1. Checking prerequisites..."
if ! command -v kubectl &>/dev/null; then
    echo -e "${RED}Error: kubectl not found in PATH${NC}" >&2
    exit 1
fi
echo -e "${GREEN}  kubectl found${NC}"

if ! kubectl cluster-info &>/dev/null; then
    echo -e "${RED}Error: cannot reach Kubernetes cluster${NC}" >&2
    exit 1
fi
echo -e "${GREEN}  cluster is reachable${NC}"
echo ""

# --- Resolve namespace ---

if [[ -z "$NAMESPACE" ]]; then
    NAMESPACE=$(kubectl config view --minify -o jsonpath='{..namespace}' 2>/dev/null || true)
    NAMESPACE="${NAMESPACE:-default}"
fi

echo "2. Using namespace '${NAMESPACE}'"
echo ""

# --- Discover engine pods ---

echo "3. Discovering running engine pods..."
POD_LIST=$(kubectl get pods -n "$NAMESPACE" \
    -l "$SELECTOR" \
    --field-selector=status.phase=Running \
    -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}' 2>/dev/null || true)

if [[ -z "$POD_LIST" ]]; then
    echo -e "${RED}Error: no running engine pods found in namespace '${NAMESPACE}'${NC}" >&2
    echo "  Ensure engine pods are deployed and running:" >&2
    echo "  kubectl get pods -n ${NAMESPACE} -l ${SELECTOR}" >&2
    exit 1
fi

# Read pod names into array
PODS=()
while IFS= read -r pod; do
    [[ -z "$pod" ]] && continue
    PODS+=("$pod")
done <<< "$POD_LIST"

POD_COUNT=${#PODS[@]}
DOMAIN_COUNT=${#DOMAINS[@]}
echo -e "${GREEN}  found ${POD_COUNT} running engine pod(s)${NC}"
echo -e "  ${DOMAIN_COUNT} domain(s) to enumerate"
echo ""

# --- Prepare output directory ---

mkdir -p .output

if ! grep -qxF '.output/' .gitignore 2>/dev/null; then
    echo '.output/' >> .gitignore
    echo -e "${CYAN}  added .output/ to .gitignore${NC}"
fi

# --- Dry run ---

if [[ "$DRY_RUN" == true ]]; then
    echo "4. Dry-run plan:"
    echo ""
    echo -e "  ${BOLD}Engine pods (${POD_COUNT}):${NC}"
    for pod in "${PODS[@]}"; do
        echo -e "    ${CYAN}${pod}${NC}"
    done
    echo ""
    echo -e "  ${BOLD}Domains (${DOMAIN_COUNT}):${NC}"
    for domain in "${DOMAINS[@]}"; do
        echo "    ${domain}"
    done
    echo ""
    if [[ $DOMAIN_COUNT -gt $POD_COUNT ]]; then
        echo -e "  ${YELLOW}Note: ${DOMAIN_COUNT} domains > ${POD_COUNT} engines — $(( DOMAIN_COUNT - POD_COUNT )) domain(s) will queue${NC}"
    fi
    echo ""
    echo -e "  ${BOLD}Command per domain:${NC}"
    echo "    kubectl exec <pod> -n ${NAMESPACE} -c engine -- \\"
    echo "      /bin/enum --config /mnt/amass-data/data/engine/config.yaml -d <domain> \\"
    echo "      -engine \"http://127.0.0.1:4000\" -rigid -active"
    echo ""
    echo "  Output: .output/<domain>_<timestamp>.log"
    echo ""
    echo -e "${GREEN}Dry run complete — no commands executed${NC}"
    exit 0
fi

# --- FIFO-based worker pool ---

TIMESTAMP=$(date -u +%Y%m%d_%H%M%S)
FIFO_PATH=$(mktemp -u /tmp/amass-pool.XXXXXX)
mkfifo "$FIFO_PATH"
exec 3<>"$FIFO_PATH"
FD3_OPEN=true

cleanup() {
    echo ""
    echo -e "${YELLOW}Cleaning up...${NC}"
    # Kill all background jobs
    jobs -p 2>/dev/null | xargs -r kill 2>/dev/null || true
    wait 2>/dev/null || true
    # Close FD 3 and remove FIFO
    if [[ "$FD3_OPEN" == true ]]; then
        exec 3>&- 2>/dev/null || true
        FD3_OPEN=false
    fi
    [[ -n "$FIFO_PATH" && -p "$FIFO_PATH" ]] && rm -f "$FIFO_PATH"
}
trap cleanup EXIT INT TERM

# Seed the pool with engine pod names
for pod in "${PODS[@]}"; do
    echo "$pod" >&3
done

echo "4. Running enumerations..."
echo ""

STATUS_DIR=".output/.status_${TIMESTAMP}"
mkdir -p "$STATUS_DIR"

for domain in "${DOMAINS[@]}"; do
    # Block until an engine pod is free
    read -r pod <&3

    (
        local_exit=0
        log_file=".output/${domain}_${TIMESTAMP}.log"
        echo -e "${CYAN}  [START] ${domain} -> ${pod}${NC}"

        kubectl exec "$pod" -n "$NAMESPACE" -c engine -- \
            /bin/enum --config /.config/amass/config.yaml \
            -d "$domain" \
            -engine "http://127.0.0.1:4000" \
            -rigid -active \
            > "$log_file" 2>&1 \
            || local_exit=$?

        # Write status sidecar file
        if [[ $local_exit -eq 0 ]]; then
            echo "SUCCESS,${pod},${log_file}" > "${STATUS_DIR}/${domain}"
            echo -e "${GREEN}  [DONE]  ${domain} (${pod}) — SUCCESS${NC}"
        else
            echo "FAILED,${pod},${log_file}" > "${STATUS_DIR}/${domain}"
            echo -e "${RED}  [DONE]  ${domain} (${pod}) — FAILED (exit ${local_exit})${NC}"
        fi

        # Return pod to the pool
        echo "$pod" >&3
    ) &
done

# Wait for all background enumerations to finish
wait

echo ""

# --- Summary ---

echo "5. Summary"
echo ""
printf "${BOLD}%-40s %-10s %-40s %s${NC}\n" "DOMAIN" "STATUS" "POD" "LOG FILE"
printf "%-40s %-10s %-40s %s\n" \
    "$(printf '%0.s-' {1..40})" \
    "$(printf '%0.s-' {1..10})" \
    "$(printf '%0.s-' {1..40})" \
    "$(printf '%0.s-' {1..20})"

ANY_FAILED=false
for domain in "${DOMAINS[@]}"; do
    status_file="${STATUS_DIR}/${domain}"
    if [[ -f "$status_file" ]]; then
        IFS=',' read -r status pod log_file < "$status_file"
        if [[ "$status" == "SUCCESS" ]]; then
            COLOR="$GREEN"
        else
            COLOR="$RED"
            ANY_FAILED=true
        fi
        printf "${COLOR}%-40s %-10s %-40s %s${NC}\n" "$domain" "$status" "$pod" "$log_file"
    else
        printf "${RED}%-40s %-10s %-40s %s${NC}\n" "$domain" "UNKNOWN" "N/A" "N/A"
        ANY_FAILED=true
    fi
done

# Clean up status directory
rm -rf "$STATUS_DIR"

echo ""
if [[ "$ANY_FAILED" == true ]]; then
    echo -e "${RED}Some enumerations failed. Check log files for details.${NC}"
    exit 1
else
    echo -e "${GREEN}All enumerations completed successfully.${NC}"
    exit 0
fi
