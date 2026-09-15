// Isolated, in-memory review fixture. Never used by npm start or Docker.
import { createDemoServer } from '../server.mjs';import { passwordHash } from '../backend/security.mjs';import { randomBytes } from 'node:crypto';
createDemoServer({backend:{dbPath:':memory:',adminHash:await passwordHash('Test-only-password-123!'),adminUser:'admin',encryptionKey:randomBytes(32).toString('base64'),production:false}}).listen(4311,'127.0.0.1',()=>console.log('Isolated browser fixture on 4311'));
