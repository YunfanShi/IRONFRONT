export function lanAddress(address:string){
 const value=address.trim().replace(/：/g,':');if(!value)throw new Error('请输入房主的局域网 IP 或分享链接');
 let url:URL;try{url=new URL(value.includes('://')?value:`http://${value}`);}catch{throw new Error('地址格式错误：例如 http://192.168.1.20:7878');}
 if(!['http:','https:'].includes(url.protocol))throw new Error('只支持 http:// 或 https:// 房主地址');
 if(url.hostname==='0.0.0.0')throw new Error('0.0.0.0 是监听地址，请使用房主的局域网 IP');
 if(!url.port)url.port='7878';return url;
}
export async function lanJSON(url:URL,init:RequestInit={}){
 const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),4500);
 try{const response=await fetch(url,{...init,signal:controller.signal,cache:'no-store'});let result:any;try{result=await response.json();}catch{throw new Error(`主机 ${url.host} 未返回房间服务数据，请确认房主更新游戏并重新运行 npm run lan`);}if(!response.ok)throw new Error(result.error||`服务返回 HTTP ${response.status}`);return result;}
 catch(error){if(error instanceof TypeError || (error instanceof Error&&error.name==='AbortError'))throw new Error(`无法访问 ${url.host}。先让朋友在浏览器打开 http://${url.host}/；检查房主服务、电脑/浏览器本地网络权限，以及 Wi-Fi 是否隔离客户端。`);throw error;}
 finally{clearTimeout(timer);}
}
