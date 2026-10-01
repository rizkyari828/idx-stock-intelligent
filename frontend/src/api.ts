export type Request = <T>(path: string, body?: unknown, signal?: AbortSignal) => Promise<T>;
export const api: Request = async <T,>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> => {
 const response=await fetch(`/api${path}`,{signal,...(body===undefined?{}:{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)})});
 const data=await response.json();
 if(!response.ok)throw new Error(data.error??data.title??`Request failed (${response.status})`);
 return data as T;
};
export const errorMessage = (error: unknown) => error instanceof Error ? error.message : String(error);
