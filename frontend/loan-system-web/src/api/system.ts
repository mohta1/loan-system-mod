export type SystemInfo={name:string;version:string;status:string;timestampUtc:string};
const apiBase=import.meta.env.VITE_API_BASE_URL??'/api';
export async function getSystemInfo():Promise<SystemInfo>{const response=await fetch(`${apiBase}/v1/system/info`);if(!response.ok)throw new Error(`API returned ${response.status}`);return response.json() as Promise<SystemInfo>;}
