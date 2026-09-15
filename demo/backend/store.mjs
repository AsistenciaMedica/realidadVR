import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
export function openStore(path){
 if(path!==':memory:')mkdirSync(dirname(path),{recursive:true,mode:0o700});
 const db=new DatabaseSync(path,{timeout:5000});db.exec(`PRAGMA foreign_keys=ON;PRAGMA journal_mode=WAL;PRAGMA busy_timeout=5000;
 CREATE TABLE IF NOT EXISTS clients(id TEXT PRIMARY KEY,name TEXT NOT NULL,legalName TEXT NOT NULL,email TEXT NOT NULL,phone TEXT NOT NULL,contactName TEXT NOT NULL,status TEXT NOT NULL CHECK(status IN ('ACTIVE','SUSPENDED','INACTIVE')),createdAt TEXT NOT NULL,notes TEXT NOT NULL);
 CREATE TABLE IF NOT EXISTS licenses(id TEXT PRIMARY KEY,clientId TEXT NOT NULL REFERENCES clients(id),keyHash TEXT NOT NULL UNIQUE,keyCipher TEXT NOT NULL,keySuffix TEXT NOT NULL,status TEXT NOT NULL CHECK(status IN ('ACTIVE','SUSPENDED','EXPIRED','REVOKED','PENDING')),createdAt TEXT NOT NULL,activatedAt TEXT,expiresAt TEXT,maxDevices INTEGER NOT NULL CHECK(maxDevices BETWEEN 1 AND 1000),notes TEXT NOT NULL);
 CREATE INDEX IF NOT EXISTS licenses_client ON licenses(clientId);
 CREATE TABLE IF NOT EXISTS activations(id TEXT PRIMARY KEY,licenseId TEXT NOT NULL REFERENCES licenses(id),deviceId TEXT NOT NULL,deviceName TEXT NOT NULL,platform TEXT NOT NULL,activatedAt TEXT NOT NULL,lastSeenAt TEXT NOT NULL,status TEXT NOT NULL CHECK(status IN ('ACTIVE','INACTIVE')),UNIQUE(licenseId,deviceId));
 CREATE INDEX IF NOT EXISTS active_devices ON activations(licenseId,status);
 CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY,createdAt TEXT NOT NULL,action TEXT NOT NULL,entityId TEXT NOT NULL);
 CREATE TABLE IF NOT EXISTS inquiries(id TEXT PRIMARY KEY,name TEXT NOT NULL,company TEXT NOT NULL,email TEXT NOT NULL,phone TEXT NOT NULL,message TEXT NOT NULL,createdAt TEXT NOT NULL);
 PRAGMA user_version=1;`);return db;
}
