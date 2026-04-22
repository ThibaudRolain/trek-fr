# syntax=docker/dockerfile:1.7
# -----------------------------
# Stage 1 — build Angular
# -----------------------------
FROM node:lts AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production

# -----------------------------
# Stage 2 — nginx runtime
# -----------------------------
FROM nginx:alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf

# La sortie d'`@angular/build:application` est /src/dist/frontend/browser/
COPY --from=build /src/dist/frontend/browser/ /usr/share/nginx/html/

EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
