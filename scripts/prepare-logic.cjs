const fs=require('node:fs');fs.mkdirSync('.logic-build',{recursive:true});fs.writeFileSync('.logic-build/package.json','{"type":"commonjs"}');
