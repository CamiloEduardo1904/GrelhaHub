# Frontend do GrelhaHub — build multi-stage: Node compila, Nginx serve o estático.
# A imagem final não contém Node nem node_modules.

# ---------- build ----------
FROM node:22-alpine AS build
WORKDIR /app

# Copia os manifestos primeiro: mudança de código não invalida o cache do npm ci.
COPY package.json package-lock.json* ./
RUN npm ci

COPY . .
RUN npm run build

# ---------- runtime ----------
FROM nginx:1.27-alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx-spa.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://127.0.0.1/ || exit 1
