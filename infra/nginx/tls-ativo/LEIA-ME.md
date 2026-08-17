# `tls-ativo/`

Diretório **propositalmente vazio**. O nginx da borda faz `include` dele com
`*` no fim do caminho, o que torna o include opcional: sem arquivo aqui, o
nginx sobe em HTTP puro e o site funciona.

`infra/ativar-https.sh` escreve dois arquivos aqui depois de o certbot emitir o
certificado:

- `redirecionar.conf` — passa a redirecionar HTTP para HTTPS
- `servidor.conf` — o bloco `server` na 443, com o certificado

Isso resolve o ovo-e-galinha: o certbot precisa do nginx respondendo em HTTP
para o desafio ACME, e o nginx não sobe apontando para um certificado que ainda
não existe.
