# Drasi Deployment Reference
This folder contains the opinionated infrastructure for the emergency-alerts Drasi integration: CDC sources, continuous queries, and HTTP-backed reactions. Follow the steps below before rolling new queries or reactions to any environment.

## 1. Drasi CLI prerequisites
- Always check the Drasi CLI version that matches the target control plane: `drasi version`. Bake the version into your pipelines (e.g., `drasi 0.10.3`) so YAML semantics stay stable.
- Use `drasi login`/`drasi context set` as needed before applying resources. We operate in `drasi-system` unless otherwise noted by the environment docs.
- Prefer `drasi apply -f <file>` and `drasi wait -f <file>` to ensure each resource becomes ready before moving on.

## 2. Deployment order (Source → ContinuousQuery → Reaction)
1. **Source** (`infrastructure/drasi/sources/postgres-cdc.yaml`)  
   ```bash
   drasi apply -f infrastructure/drasi/sources/postgres-cdc.yaml
   drasi wait -f infrastructure/drasi/sources/postgres-cdc.yaml -t 120
   ```
   Verify with `drasi source describe postgres-alerts` and confirm the CDC slots/table mappings.
2. **Continuous queries** (`infrastructure/drasi/queries/emergency-alerts.yaml`)  
   ```bash
   drasi apply -f infrastructure/drasi/queries/emergency-alerts.yaml
   drasi wait -f infrastructure/drasi/queries/emergency-alerts.yaml -t 120
   ```
   After the apply completes, inspect query status (`drasi query describe <query-name>`) and look for `status: active`. When you change a query, version it by incrementing the query name suffix in Git instead of patching in place, unless the change is non-disruptive.
3. **Reactions** (`infrastructure/drasi/reactions/emergency-alerts-http.yaml`)  
   ```bash
   drasi apply -f infrastructure/drasi/reactions/emergency-alerts-http.yaml
   drasi wait -f infrastructure/drasi/reactions/emergency-alerts-http.yaml -t 120
   ```
   Reactions run after queries see new results; make sure their HTTP targets are reachable before you deploy (see `DrasiReactionsController` in the API).

## 3. HTTP reaction configuration
- The reaction resource above resolves to the API’s `POST /api/v1/drasi/reactions/{query}` endpoints. Update `DRASI_HTTP_REACTION_BASE_URL` (Kubernetes `ConfigMap`) if the base URL changes. Reactions can include the `X-Reaction-Token` header with the same value as the `drasi-reaction-auth` secret (see `infrastructure/drasi/secrets/drasi-reaction-auth.yaml`) when you want to validate callbacks. The backend configuration key `Drasi:ReactionAuthToken` should point to that secret if enabled, e.g.:
  ```yaml
  env:
    - name: Drasi__ReactionAuthToken
      valueFrom:
        secretKeyRef:
          name: drasi-reaction-auth
          key: token
  ```
- Drasi performs token-based authentication for these requests. Create a secret such as:
  ```bash
  kubectl create secret generic drasi-reaction-auth \
    --from-literal=token=$(openssl rand -hex 32) \
    -n emergency-alerts
  ```
  Reference that token in your deployment manifest or pipeline variables so the API can validate incoming requests (the API logs include the correlation ID and reaction name).
- Each query node (delivery trigger, SLA breach, approval timeout, etc.) includes a templated JSON payload so the API can reconstruct the domain entities and re-invoke the reaction handlers idempotently. See the `DrasiReactionsController` and the reaction YAML for the exact payload contracts.

## 4. Verification checklist
- After applying sources/queries/reactions:
  - `drasi query describe <name>` → `status: active`, `lastSuccess` updated.
  - `kubectl logs -n emergency-alerts deployment/emergency-alerts-api | grep ReactionHandler` to verify the backend accepted the callback.
  - `_DrasiApiHealth` endpoints (`/health/ready` and `/health/live`) report `DrasiHealthy: true`.
- When changing a query or reaction:
  - Run `drasi wait -f <file> -t 120` to ensure the new definition stabilizes.
  - Update `infrastructure/drasi/perf-test.sh` or your CI job if you change query names or reaction payloads.

## 5. Troubleshooting tips
- If a reaction is missing, confirm the Reaction name matches the query (`delivery-trigger`, `geographic-correlation`, etc.) and that the source labels match the `DrasiReactionsController` expectations.
- Use the Drasi CLI to inspect reaction backoff/resend history: `drasi reaction describe <reaction-name>`.
- Reaction endpoints payloads are logged at `Information` level so you can replay them; refer to the `DrasiReactionHandlers` log snippets in `infrastructure/docs/runbooks/dashboard-troubleshooting.md`.

Keep this file updated as you add or remove queries/reactions.
