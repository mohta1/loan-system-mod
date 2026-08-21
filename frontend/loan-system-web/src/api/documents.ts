import{ApiError}from'./identity';
export type DocumentStatus='Active'|'DeletePending'|'Deleted';
export type DocumentMetadata={documentId:string;fileName:string;contentType:string;size:number;uploadedAt:string;status:DocumentStatus};
async function request(url:string,init?:RequestInit){let response:Response;try{response=await fetch(url,{...init,credentials:'same-origin'})}catch{throw new ApiError(0,'network')}
 if(response.ok)return response;let code:string|undefined;try{code=(await response.json() as {errorCode?:string}).errorCode}catch{/* invalid problem body */}if(response.status===401)window.dispatchEvent(new Event('identity:unauthorized'));throw new ApiError(response.status,code)}
export const documentsApi={
 upload:async(file:File)=>{const form=new FormData();form.append('file',file);return (await request('/api/v1/documents',{method:'POST',body:form})).json() as Promise<DocumentMetadata>},
 download:async(id:string)=>(await request(`/api/v1/documents/${id}/content`)).blob(),
 remove:async(id:string)=>{await request(`/api/v1/documents/${id}`,{method:'DELETE'})}
};
