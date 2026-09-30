import type { ComponentPropsWithRef, ElementType, ReactNode } from 'react';

import styles from './Container.module.css';

type ContainerElement = ElementType;

interface ContainerProps extends ComponentPropsWithRef<ContainerElement> {
  as?: ContainerElement;
  children: ReactNode;
}

export function Container({ as: Component = 'div', className, children, ...rest }: ContainerProps) {
  const classes = className ? `${styles.container} ${className}` : styles.container;

  return (
    <Component className={classes} {...rest}>
      {children}
    </Component>
  );
}
