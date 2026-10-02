export type Request = <T>(path: string, body?: unknown, signal?: AbortSignal) => Promise<T>;
export class ApiError extends Error {
 constructor(message: string, readonly status: number, readonly code?: string) { super(message); }
}
export const api: Request = async <T,>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> => {
 const response=await fetch(`/api${path}`,{signal,...(body===undefined?{}:{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)})});
 const data=await response.json();
 if(!response.ok)throw new ApiError(data.error??data.title??`Request failed (${response.status})`,response.status,data.code);
 return data as T;
};
export const errorMessage = (error: unknown) => error instanceof Error ? error.message : String(error);
