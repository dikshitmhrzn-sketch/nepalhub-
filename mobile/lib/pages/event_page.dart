import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/api/api_exception.dart';
import '../../core/auth/session_gate.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/external.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/loadable_view.dart';
import '../../core/widgets/report_action.dart';
import '../../features/bookings/api.dart';
import '../../features/events/api.dart';
import '../../features/payments/api.dart';

class EventPage extends StatefulWidget {
  const EventPage({super.key, required this.slug});

  final String slug;

  @override
  State<EventPage> createState() => _EventPageState();
}

class _EventPageState extends State<EventPage> {
  int _quantity = 1;
  bool _submitting = false;
  Booking? _booking;
  String? _message;

  Future<void> _book() async {
    if (!await ensureSignedIn(context)) {
      return;
    }
    if (!mounted) {
      return;
    }
    final api = BookingsApi(context.read<ApiClient>());
    setState(() {
      _submitting = true;
      _message = null;
    });
    try {
      final booking = await api.book(widget.slug, quantity: _quantity);
      if (!mounted) {
        return;
      }
      setState(() {
        _booking = booking;
        _submitting = false;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _message = ApiMessage.message(error);
      });
    }
  }

  Future<void> _release() async {
    final api = BookingsApi(context.read<ApiClient>());
    setState(() {
      _submitting = true;
      _message = null;
    });
    try {
      await api.cancel(widget.slug);
      if (!mounted) {
        return;
      }
      setState(() {
        _booking = null;
        _submitting = false;
        _message = 'Your place has been released.';
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _message = ApiMessage.message(error);
      });
    }
  }

  void _changeQuantity(int delta) {
    setState(() {
      final next = _quantity + delta;
      if (next >= 1 && next <= 4) {
        _quantity = next;
      }
    });
  }

  Future<void> _startCheckout() async {
    if (!await ensureSignedIn(context)) {
      return;
    }
    if (!mounted) {
      return;
    }
    final api = PaymentsApi(context.read<ApiClient>());
    setState(() {
      _submitting = true;
      _message = null;
    });
    try {
      final checkout = await api.checkout(
        kind: 'EventTicket',
        slug: widget.slug,
        quantity: _quantity,
      );
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      if (!checkout.needsForm) {
        setState(() => _message = 'That payment has already finished.');
        return;
      }
      await context.push('/checkout', extra: checkout);
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _message = ApiMessage.message(error);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final api = EventsApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Event')),
      body: LoadableView<EventDetail>(
        load: () => api.detail(widget.slug),
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
                value: event.capacity == 0
                    ? 'Unlimited'
                    : '${event.remainingSpots} of ${event.capacity} left',
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
              const SizedBox(height: NhSpace.s5),
              _buildBooking(theme, colors, event),
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
              const SizedBox(height: NhSpace.s5),
              ReportAction(targetType: 'Event', targetId: event.id),
            ],
          );
        },
      ),
    );
  }

  Widget _buildBooking(ThemeData theme, ColorScheme colors, EventDetail event) {
    final booked = _booking;
    final seatsLeft = event.capacity == 0 || event.remainingSpots > 0;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(NhSpace.s3),
        child: booked == null
            ? Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Book your place',
                    style: theme.textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    event.capacity == 0
                        ? 'This event has no fixed capacity.'
                        : '${event.remainingSpots} of ${event.capacity} places left.',
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: colors.onSurfaceVariant),
                  ),
                  const SizedBox(height: NhSpace.s3),
                  Row(
                    children: [
                      IconButton(
                        tooltip: 'One place fewer',
                        onPressed: _submitting || _quantity == 1
                            ? null
                            : () => _changeQuantity(-1),
                        icon: const Icon(Icons.remove_circle_outline_rounded),
                      ),
                      Text(
                        _quantity == 1 ? '1 place' : '$_quantity places',
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(fontWeight: FontWeight.w700),
                      ),
                      IconButton(
                        tooltip: 'One place more',
                        onPressed: _submitting || _quantity == 4
                            ? null
                            : () => _changeQuantity(1),
                        icon: const Icon(Icons.add_circle_outline_rounded),
                      ),
                    ],
                  ),
                  const SizedBox(height: NhSpace.s2),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: !_submitting && seatsLeft
                          ? (event.ticketPrice == 0 ? _book : _startCheckout)
                          : null,
                      child: _submitting
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : Text(
                              event.ticketPrice == 0
                                  ? 'Book my place'
                                  : 'Buy a place  ·  ${npr(event.ticketPrice)}',
                            ),
                    ),
                  ),
                  if (_message != null)
                    Padding(
                      padding: const EdgeInsets.only(top: NhSpace.s2),
                      child: Text(
                        _message!,
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: colors.error),
                      ),
                    ),
                ],
              )
            : Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    booked.quantity == 1
                        ? 'You hold 1 place at this event'
                        : 'You hold ${booked.quantity} places at this event',
                    style: theme.textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    'Booked on ${formatDay(booked.createdAt)}.',
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: colors.onSurfaceVariant),
                  ),
                  if (_message != null)
                    Padding(
                      padding: const EdgeInsets.only(top: NhSpace.s1),
                      child: Text(
                        _message!,
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.error),
                      ),
                    ),
                  const SizedBox(height: NhSpace.s3),
                  SizedBox(
                    width: double.infinity,
                    child: OutlinedButton(
                      onPressed: _submitting ? null : _release,
                      child: _submitting
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Release my place'),
                    ),
                  ),
                ],
              ),
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
