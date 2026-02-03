# Emergency Alerts Frontend

React 19 + Vite + TypeScript frontend for the Emergency Alerts system.

## Build

```bash
npm install
npm run dev      # http://localhost:5173
npm run build    # Production bundle
npm run test     # Unit tests
```

## Docker

**Vite bakes environment variables at build time.** Pass `VITE_API_URL` when building:

```bash
docker build -t emergency-alerts-frontend --build-arg VITE_API_URL=https://api.example.com .
```

If you forget, the frontend will call the wrong backend. There's no runtime override.

## Verify the build

```bash
# Check what URL got baked in
kubectl exec <pod> -- cat /app/dist/assets/index-*.js | grep -o 'https://[^"]*api[^"]*'
```

## Lint

```bash
npm run lint
```

Type-aware lint rules are available in eslint.config.js if you want stricter checks.
