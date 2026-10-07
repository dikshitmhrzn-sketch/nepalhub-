import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/external.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/loadable_view.dart';
import '../../features/events/api.dart';

class EventPage extends StatelessWidget {
  const EventPage({super.key, required this.slug});

  final String slug;

  @override
  Widget build(BuildContext context) {
    final api = EventsApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Event')),
      body: LoadableView<EventDetail>(
        load: () => api.detail(slug),
        builder: (context, event, _) {
          final place = [
            if ((event.venue ?? '').trim().isNotEmpty) event.venue!,
            if ((event.districtName ?? '').trim().isNotEmpty) event.districtName!,
            if ((event.provinceName ?? '').trim().isNotEmpty) event.provinceName!,
          ].join(', ');
          final registrationUrl = event.registrationUrl;
          return ListView(
            padding: const EdgeInsets.all(NhSpace.s4),
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      event.title,
                      style: theme.textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  if (event.isOngoing)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: NhSpace.s2,
                        vertical: 3,
                      ),
                      decoration: BoxDecoration(
                        color: const Color(0xFF128A4C).withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        'Happening now',
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: const Color(0xFF128A4C),
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                ],
              ),
              const SizedBox(height: NhSpace.s2),
              Wrap(
                spacing: NhSpace.s2,
                runSpacing: NhSpace.s1,
                children: [
                  _Pill(label: humanize(event.category)),
                  if (event.businessName != null && event.businessName!.trim().isNotEmpty)
                    _Pill(label: event.businessName!),
                ],
              ),
              const SizedBox(height: NhSpace.s3),
              _Fact(label: 'When', value: formatRange(event.startsAt, event.endsAt)),
              if (place.isNotEmpty) _Fact(label: 'Where', value: place),
              if (event.organiser != null && event.organiser!.trim().isNotEmpty)
                _Fact(label: 'Organiser', value: event.organiser!),
              _Fact(label: 'Tickets', value: event.ticketPrice == 0 ? 'Free' : npr(event.ticketPrice)),
              _Fact(
                label: 'Places',
                value: '${event.remainingSpots} of ${event.capacity} left',
              ),
              if (event.description.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s4),
                Text(
                  'About',
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: NhSpace.s2),
                Text(event.description, style: theme.textTheme.bodyMedium),
              ],
              if (event.businessSlug != null && event.businessSlug!.isNotEmpty) ...[
                const SizedBox(height: NhSpace.s4),
                OutlinedButton.icon(
                  onPressed: () => context.push('/businesses/${event.businessSlug}'),
                  icon: const Icon(Icons.storefront_outlined, size: 18),
                  label: const Text('View the organiser'),
                ),
              ],
              const SizedBox(height: NhSpace.s4),
              FilledButton.icon(
                onPressed: registrationUrl != null && registrationUrl.trim().isNotEmpty
                    ? () => openExternal(context, registrationUrl)
                    : null,
                icon: const Icon(Icons.open_in_new_rounded, size: 18),
                label: const Text('Open the registration page'),
              ),
              if (registrationUrl == null || registrationUrl.trim().isEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: NhSpace.s2),
                  child: Text(
                    'This event does not link to an external registration page.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (value.trim().isEmpty) {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: NhSpace.s1),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final tint = Theme.of(context).colorScheme.onSurfaceVariant;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: NhSpace.s2, vertical: 3),
      decoration: BoxDecoration(
        color: tint.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: tint,
              fontWeight: FontWeight.w600,
            ),
      ),
    );
  }
}
