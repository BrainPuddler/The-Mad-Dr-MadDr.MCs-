# Build stage — compile genome-core then mutator-service
FROM node:22-alpine AS build
WORKDIR /app

# Render auto-populates this build arg with the deployed commit's SHA for
# any Docker service that declares it (no render.yaml wiring needed) --
# baked in here so a stale deploy is visible from the outside via
# /version instead of only discoverable by diffing git log against
# observed behavior (see docs/12 decision log).
ARG RENDER_GIT_COMMIT=unknown

# genome-core: install deps + compile
COPY packages/genome-core/package*.json ./packages/genome-core/
RUN cd packages/genome-core && npm ci

COPY packages/genome-core/src         ./packages/genome-core/src
COPY packages/genome-core/tsconfig.json ./packages/genome-core/
RUN cd packages/genome-core && npm run build

# mutator-service: install deps (resolves file:../genome-core) + compile
COPY packages/mutator-service/package*.json ./packages/mutator-service/
RUN cd packages/mutator-service && npm ci

COPY packages/mutator-service/src          ./packages/mutator-service/src
COPY packages/mutator-service/tsconfig.json ./packages/mutator-service/
RUN cd packages/mutator-service && npm run build

# Runtime stage — only compiled output, no dev tools
FROM node:22-alpine
WORKDIR /app

# ARGs don't cross the FROM boundary -- redeclare, then bake into an ENV
# so the running process can read it (Render also sets RENDER_GIT_COMMIT
# directly at runtime; BUILD_COMMIT is the fallback for that not being
# present, and for plain `docker run` outside Render).
ARG RENDER_GIT_COMMIT=unknown
ENV BUILD_COMMIT=$RENDER_GIT_COMMIT

# genome-core: package.json + compiled dist (no node_modules — zero runtime deps)
COPY --from=build /app/packages/genome-core/package.json ./packages/genome-core/
COPY --from=build /app/packages/genome-core/dist         ./packages/genome-core/dist

# mutator-service: install prod deps only (npm resolves file:../genome-core → symlink above)
COPY --from=build /app/packages/mutator-service/package*.json ./packages/mutator-service/
RUN cd packages/mutator-service && npm ci --omit=dev

COPY --from=build /app/packages/mutator-service/dist ./packages/mutator-service/dist

WORKDIR /app/packages/mutator-service
ENV NODE_ENV=production PORT=8787
EXPOSE 8787
CMD ["node", "dist/src/server.js"]
