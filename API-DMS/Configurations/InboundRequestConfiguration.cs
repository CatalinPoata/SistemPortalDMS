using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class InboundRequestConfiguration
        : BaseEntityConfiguration<InboundRequest>
    {
        public override void Configure(
            EntityTypeBuilder<InboundRequest> builder)
        {
            base.Configure(builder);

            builder.ToTable("inbound_request", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_inbound_request_endpoint_length",
                    "length(endpoint) <= 100");

                table.HasCheckConstraint(
                    "CK_inbound_request_idempotency_key_length",
                    "length(idempotency_key) <= 100");

                table.HasCheckConstraint(
                    "CK_inbound_request_hash_length",
                    "length(request_hash) = 64");

                table.HasCheckConstraint(
                    "CK_inbound_request_response_status",
                    "response_status >= 100 AND response_status <= 599");
            });

            builder.HasKey(e => new
            {
                e.endpoint,
                e.idempotency_key
            });

            builder.Property(e => e.endpoint)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.idempotency_key)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.request_hash)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.response_status)
                .IsRequired();

            builder.Property(e => e.response_body)
                .HasColumnType("jsonb")
                .IsRequired();
        }
    }
}
