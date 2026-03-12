# Helm Chart Guide

Installation, configuration, and deployment scenarios for the `amass-conductor` Helm chart.

## Prerequisites

- [Helm 3](https://helm.sh/docs/intro/install/)
- Kubernetes 1.26+
- The engine namespace (default `amass`) must already exist
- A container image for `amass-conductor` available to your cluster (local registry, Docker Hub, or private registry)

## Installation

### Basic install

```bash
helm install amass-conductor ./charts/amass-conductor
```

### Custom values file

```bash
helm install amass-conductor ./charts/amass-conductor -f my-values.yaml
```

### Post-install access

Port-forward the service to access the UI locally:

```bash
kubectl port-forward svc/amass-conductor 8080:80
```

Then open `http://localhost:8080` in your browser.

## Configuration Reference

All configuration is set via `values.yaml`. Override any key with `--set` flags or a custom values file.

### Image

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `replicaCount` | int | `1` | Number of pod replicas |
| `image.repository` | string | `amass-conductor` | Container image repository |
| `image.pullPolicy` | string | `IfNotPresent` | Image pull policy |
| `image.tag` | string | `""` (chart appVersion) | Image tag override |
| `imagePullSecrets` | list | `[]` | Docker registry secrets |

### Naming

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `nameOverride` | string | `""` | Override the chart name |
| `fullnameOverride` | string | `""` | Override the full release name |

### Service Account & RBAC

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `serviceAccount.create` | bool | `true` | Create a ServiceAccount |
| `serviceAccount.automount` | bool | `true` | Auto-mount the ServiceAccount token |
| `serviceAccount.annotations` | object | `{}` | Annotations for the ServiceAccount |
| `serviceAccount.name` | string | `""` | Override ServiceAccount name |
| `rbac.create` | bool | `true` | Create Role and RoleBinding for engine namespace |
| `rbac.engineNamespace` | string | `amass` | Namespace where engine pods run |

### Pod Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `podAnnotations` | object | `{}` | Extra annotations for pods |
| `podLabels` | object | `{}` | Extra labels for pods |
| `podSecurityContext` | object | `{}` | Pod-level security context |
| `securityContext` | object | `{}` | Container-level security context |
| `resources` | object | `{}` | CPU/memory requests and limits |

### Service

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `service.type` | string | `ClusterIP` | Kubernetes Service type |
| `service.port` | int | `80` | Service port |

### Ingress

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ingress.enabled` | bool | `false` | Enable Ingress resource |
| `ingress.className` | string | `""` | Ingress class name |
| `ingress.annotations` | object | `{}` | Ingress annotations |
| `ingress.hosts` | list | see below | Host rules |
| `ingress.tls` | list | `[]` | TLS configuration |

Default host configuration:

```yaml
ingress:
  hosts:
    - host: amass-conductor.local
      paths:
        - path: /
          pathType: Prefix
```

### Orchestrator

These values map to `Orchestrator__*` environment variables in the container.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `orchestrator.namespace` | string | `amass` | Kubernetes namespace for engine pods |
| `orchestrator.statefulSetName` | string | `amass-engine` | StatefulSet name to monitor |
| `orchestrator.enginePort` | int | `4000` | Port where engine API listens |
| `orchestrator.pollIntervalSeconds` | int | `2` | Seconds between polling cycles |
| `orchestrator.labelSelector` | string | `app.kubernetes.io/name=amass,...` | Label selector for pod discovery |
| `orchestrator.engineBasePath` | string | `/api/v1` | Base path for engine REST API |
| `orchestrator.httpClientTimeoutSeconds` | int | `10` | HTTP client timeout |
| `orchestrator.webSocketBufferSize` | int | `4096` | WebSocket receive buffer size |
| `orchestrator.completionPollThreshold` | int | `5` | Consecutive completed polls before marking done |
| `orchestrator.databasePath` | string | `App_Data/orchestrator.db` | SQLite database file path |
| `orchestrator.maxActiveSessionsPerEngine` | int | `1` | Max concurrent sessions per engine |
| `orchestrator.autoDeleteCompletedSessions` | bool | `true` | Auto-delete completed sessions from engine |

### ASP.NET Core

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `aspnetcore.environment` | string | `Production` | ASP.NET Core environment |
| `aspnetcore.urls` | string | `http://+:8080` | Listener URL |

### Persistence

#### Data volume

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `persistence.data.enabled` | bool | `true` | Enable data PVC (SQLite database) |
| `persistence.data.size` | string | `1Gi` | Volume size |
| `persistence.data.accessMode` | string | `ReadWriteOnce` | PVC access mode |
| `persistence.data.storageClass` | string | `""` | Storage class (empty = cluster default) |
| `persistence.data.existingClaim` | string | `""` | Use an existing PVC instead of creating one |

#### Logs volume

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `persistence.logs.enabled` | bool | `false` | Enable logs PVC |
| `persistence.logs.size` | string | `5Gi` | Volume size |
| `persistence.logs.accessMode` | string | `ReadWriteOnce` | PVC access mode |
| `persistence.logs.storageClass` | string | `""` | Storage class (empty = cluster default) |
| `persistence.logs.existingClaim` | string | `""` | Use an existing PVC instead of creating one |

### Scheduling

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `nodeSelector` | object | `{}` | Node selector constraints |
| `tolerations` | list | `[]` | Tolerations for taints |
| `affinity` | object | `{}` | Affinity rules |

### Extra

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `extraEnv` | list | `[]` | Additional environment variables |

## Common Deployment Scenarios

### Minimal production

```yaml
image:
  repository: registry.example.com/amass-conductor
  tag: "1.0.0"

resources:
  requests:
    cpu: 100m
    memory: 256Mi
  limits:
    cpu: 500m
    memory: 512Mi
```

### With ingress and TLS

```yaml
ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
  hosts:
    - host: amass.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: amass-conductor-tls
      hosts:
        - amass.example.com
```

### Custom engine namespace

```yaml
rbac:
  engineNamespace: recon-engines

orchestrator:
  namespace: recon-engines
  statefulSetName: my-amass-engine
  labelSelector: "app=amass-engine"
```

### External registry with pull secrets

```yaml
image:
  repository: registry.example.com/amass-conductor
  tag: "1.0.0"
  pullPolicy: Always

imagePullSecrets:
  - name: registry-credentials
```

### Log persistence

```yaml
persistence:
  data:
    enabled: true
    size: 2Gi
  logs:
    enabled: true
    size: 10Gi
    storageClass: gp3
```

### Existing PVC

```yaml
persistence:
  data:
    enabled: true
    existingClaim: my-existing-data-pvc
  logs:
    enabled: true
    existingClaim: my-existing-logs-pvc
```

## RBAC and Cross-Namespace Access

Amass Conductor runs in its own namespace but needs to discover engine pods in a different namespace (default `amass`). The chart handles this with a cross-namespace RBAC setup:

1. **Role** — created in the engine namespace (`rbac.engineNamespace`) granting `get`, `list`, and `watch` on pods
2. **RoleBinding** — created in the engine namespace, binding the Role to the Conductor's ServiceAccount in the release namespace

This gives the Conductor the minimum permissions needed: read-only access to pods in the engine namespace. The Conductor never needs write access to pods, and it never needs access to other resource types.

If you manage RBAC externally, set `rbac.create: false` and `serviceAccount.create: false`, then provide the ServiceAccount name:

```yaml
rbac:
  create: false
serviceAccount:
  create: false
  name: my-precreated-sa
```

Ensure your external Role grants at minimum:

```yaml
rules:
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list", "watch"]
```

## Persistence

### Data volume (SQLite)

The data PVC stores the SQLite database (`orchestrator.db`) containing session records and logs. It is enabled by default.

Because SQLite does not support concurrent writers, the Deployment uses the **Recreate** strategy. This ensures the old pod terminates fully before the new pod starts, preventing database lock contention during upgrades.

### Logs volume

The logs PVC is optional and stores Serilog rolling JSON log files. Enable it when you need persistent log files beyond the container lifecycle.

### Storage class

Leave `storageClass` empty to use the cluster default. Set it to a specific class (e.g., `gp3`, `standard`, `local-path`) to control the backing storage.

### Existing claims

Set `persistence.data.existingClaim` or `persistence.logs.existingClaim` to use a pre-provisioned PVC. When an existing claim is specified, the chart does not create a new PVC for that volume.

### ConfigMap checksum annotation

The Deployment template includes a `checksum/config` annotation computed from the ConfigMap. This forces a pod restart whenever configuration values change, ensuring the running pod always reflects the current config.

## Upgrading

```bash
helm upgrade amass-conductor ./charts/amass-conductor -f my-values.yaml
```

**Notes:**

- The Recreate strategy means a brief downtime during upgrades — the old pod stops before the new one starts
- PVCs are retained across upgrades; your SQLite data and logs persist
- ConfigMap changes trigger a pod restart automatically via the checksum annotation

### Rollback

```bash
helm rollback amass-conductor [REVISION]
```

Check revision history with `helm history amass-conductor`.

## Uninstalling

```bash
helm uninstall amass-conductor
```

PVCs are **not** automatically deleted by `helm uninstall`. Clean them up manually if needed:

```bash
kubectl delete pvc amass-conductor-data
kubectl delete pvc amass-conductor-logs
```

## Troubleshooting

| Symptom | Likely Cause | Resolution |
|---------|-------------|------------|
| CrashLoopBackOff | Missing data PVC or incorrect image tag | Check `kubectl describe pod` for mount errors; verify `image.tag` matches an available image |
| No engines discovered | Wrong namespace or label selector | Verify `orchestrator.namespace` and `orchestrator.labelSelector` match your engine pods (`kubectl get pods -n amass -l app.kubernetes.io/name=amass`) |
| RBAC forbidden errors | Role/RoleBinding not created or wrong namespace | Ensure `rbac.create: true` and `rbac.engineNamespace` matches the engine namespace; check with `kubectl auth can-i list pods --as=system:serviceaccount:<release-ns>:<sa-name> -n <engine-ns>` |
| WebSocket disconnects | Ingress proxy timeout too short | Set `nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"` and `proxy-send-timeout: "3600"` in ingress annotations |
| Database locked errors | Multiple replicas or RollingUpdate strategy | Keep `replicaCount: 1` and verify the Deployment uses `Recreate` strategy (default) |
| Config not applied after values change | Pod not restarted | The checksum annotation should handle this automatically; if using external config, manually restart with `kubectl rollout restart deployment amass-conductor` |
