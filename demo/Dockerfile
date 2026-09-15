FROM node:24-alpine
WORKDIR /app
COPY --chown=node:node package.json server.mjs ./
COPY --chown=node:node backend ./backend
COPY --chown=node:node public ./public
COPY --chown=node:node releases ./releases
COPY --chown=node:node release*.json ./
RUN mkdir -p /app/data && chown node:node /app/data
USER node
ENV NODE_ENV=production
ENV DATABASE_PATH=/app/data/vital.sqlite
EXPOSE 4310
CMD ["node", "server.mjs"]
