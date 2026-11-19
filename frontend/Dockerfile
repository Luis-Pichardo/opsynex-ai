# syntax=docker/dockerfile:1
# Frontend de Opsynex AI: build con Node y servido con nginx. Contexto: carpeta frontend/.

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# La URL del API se fija en tiempo de build (Vite la incrusta en el bundle).
ARG VITE_API_URL=http://localhost:8080
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

FROM nginx:1.27-alpine AS runtime
# Origen del API para la CSP (connect-src). Se puede cambiar al ejecutar: -e API_ORIGIN=https://api…
ENV API_ORIGIN=http://localhost:8080
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
