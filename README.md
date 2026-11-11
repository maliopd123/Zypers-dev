# Sistema de Chaves

Aplicação web para controlar o cadastro, a retirada, a devolução e o histórico de chaves.

de como executar seu projeto caso nao funcione, precisa dessas coisas ai - Abraão
## Requisitos

- .NET 8 SDK
- MySQL ou MariaDB em execução

## Iniciar

Confira `ConnectionStrings:MySqlConnection` em `Sistemas de Chaves/appsettings.json`: ajuste o servidor, a porta e as credenciais para a instalação local do MySQL/MariaDB. A configuração atual usa a porta `3310` do MariaDB deste ambiente; instalações padrão do MySQL costumam usar `3306`. Na primeira inicialização, o aplicativo cria o banco `sistema_chaves`, aplica o script `DAO/Chaves.sql` e insere as categorias iniciais.

Execute na pasta Zypers-dev:

dotnet run --project "Sistemas de Chaves/SistemaDeChaves.csproj"

No primeiro acesso, entre com o código ADMIN001 e a senha Chaves@123. Altere BootstrapAdmin:Code, BootstrapAdmin:Email e BootstrapAdmin:Password na configuração antes de publicar o sistema.

## Funcionalidades

- Acesso por código único e senha.
- Cadastro de usuários, categorias de usuários e níveis de permissão.
- Cadastro e consulta de chaves.
- Registro de retirada e devolução com prazo.
- Painel de chaves disponíveis, em uso e em atraso.
- Histórico, relatórios, termo de responsabilidade e alertas de atraso.
