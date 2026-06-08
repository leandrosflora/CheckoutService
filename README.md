# CheckoutService

Checkout Service implementado como um orquestrador transacional leve usando ASP.NET Core Minimal APIs.

## Responsabilidades

- Criar sessões de checkout a partir de um carrinho.
- Validar comprador, vendedor, itens e endereço de entrega.
- Chamar o Shipping Promise Service para obter promessa de entrega e custo de frete.
- Persistir o estado do checkout com EF Core/PostgreSQL.
- Garantir idempotência na criação e na confirmação do checkout.
- Persistir eventos de integração em uma tabela de outbox.
- Expor status do checkout e health checks.

## Fora do escopo

Este serviço não calcula rotas, frete, SLA, tracking, criação de shipment, integrações diretas com transportadoras, emissão de etiquetas ou baixa real de estoque.

## Endpoints

- `POST /checkouts` — cria uma sessão de checkout. Requer o header `Idempotency-Key`.
- `GET /checkouts/{checkoutId}` — retorna status, totais e promessa de entrega do checkout.
- `POST /checkouts/{checkoutId}/confirm` — confirma um checkout. Requer o header `Idempotency-Key`.
- `GET /health` — expõe verificações de integridade.

## Configuração

```json
{
  "ConnectionStrings": {
    "CheckoutDb": "Host=localhost;Port=5432;Database=checkout;Username=checkout;Password=checkout"
  },
  "Services": {
    "ShippingPromise": "https://shipping-promise.local"
  }
}
```

## Publicação de eventos

O fluxo de checkout grava mensagens `CheckoutCreated` e `CheckoutConfirmed` em `outbox_messages` na mesma unidade de trabalho do EF Core usada para persistir o checkout. Um worker separado deve publicar registros pendentes da outbox no Kafka, ou em outro broker, e depois marcá-los como processados.
