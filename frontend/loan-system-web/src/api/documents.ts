import{ApiError}from'./identity';
export type DocumentMetadata={documentId:string;fileName:string;contentType:string;size:number;uploadedAt:string};
async function parse(response:Response){if(response.ok)return response;let code:string|undefined;try{code=(await response.json()).errorCode}catch{/* invalid problem body */}throw new ApiError(response.status,code)}
export const documentsApi={
 upload:async(file:File)=>{const form=new FormData();form.append('file',file);return (await parse(await fetch('/api/v1/documents',{method:'POST',body:form}))).json() as Promise<DocumentMetadata>},
 download:async(id:string)=>(await parse(await fetch(`/api/v1/documents/${id}/content`))).blob(),
 remove:async(id:string)=>{await parse(await fetch(`/api/v1/documents/${id}`,{method:'DELETE'}))}
};
