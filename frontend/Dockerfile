FROM node:22-alpine AS build
WORKDIR /app
ARG GOOGLE_SIGN_IN_ENABLED=false
ARG GOOGLE_CLIENT_ID=
ARG API_BASE_URL=/api
COPY package*.json ./
RUN npm ci
COPY . .
RUN node scripts/configure-build.mjs
RUN npm run build

FROM nginx:stable-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/erp-frontend/browser /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -q -O /dev/null http://127.0.0.1/health || exit 1
