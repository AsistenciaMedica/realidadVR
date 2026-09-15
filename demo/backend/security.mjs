import { randomBytes, scrypt, timingSafeEqual, createHash, createCipheriv, createDecipheriv } from 'node:crypto';
import { promisify } from 'node:util';
const derive=promisify(scrypt);
export const hash=value=>createHash('sha256').update(value).digest('hex');
export async function passwordHash(password){const salt=randomBytes(16).toString('hex');return salt+':'+(await derive(password,salt,64)).toString('hex');}
export async function passwordMatches(password,encoded){const [salt,key]=encoded.split(':');if(!/^[a-f0-9]{32}$/.test(salt)||!/^[a-f0-9]{128}$/.test(key))return false;const actual=await derive(password,salt,64);return timingSafeEqual(actual,Buffer.from(key,'hex'));}
export function encrypt(value,key){const iv=randomBytes(12),cipher=createCipheriv('aes-256-gcm',key,iv,{authTagLength:16});const data=Buffer.concat([cipher.update(value,'utf8'),cipher.final()]);return [iv,cipher.getAuthTag(),data].map(x=>x.toString('base64')).join('.');}
export function decrypt(value,key){const [iv,tag,data]=value.split('.').map(x=>Buffer.from(x,'base64'));const cipher=createDecipheriv('aes-256-gcm',key,iv,{authTagLength:16});cipher.setAuthTag(tag);return Buffer.concat([cipher.update(data),cipher.final()]).toString('utf8');}
export function fault(status,message){const error=new Error(message);error.status=status;return error;}
export function text(value,max=200,required=false){if(value===undefined&&!required)return '';if(typeof value!=='string'||value.length>max||(required&&!value.trim()))throw fault(400,'Campo inválido.');return value.trim();}
export function oneOf(value,values){if(!values.includes(value))throw fault(400,'Estado inválido.');return value;}
export async function body(req){if(!req.headers['content-type']?.startsWith('application/json'))throw fault(415,'Usa application/json.');let size=0;const chunks=[];for await(const chunk of req){size+=chunk.length;if(size>16384)throw fault(413,'Solicitud demasiado grande.');chunks.push(chunk);}try{const data=JSON.parse(Buffer.concat(chunks).toString('utf8'));if(!data||Array.isArray(data)||typeof data!=='object')throw Error();return data;}catch{throw fault(400,'JSON inválido.');}}
export class RateLimiter {
 constructor(){this.buckets=new Map();}
 allow(key,limit,windowMs){const now=Date.now();if(this.buckets.size>5000)for(const [key,b]of this.buckets)if(b.end<now)this.buckets.delete(key);if(this.buckets.size>10000)return false;const b=this.buckets.get(key);if(!b||b.end<now){this.buckets.set(key,{count:1,end:now+windowMs});return true;}return ++b.count<=limit;}
}
